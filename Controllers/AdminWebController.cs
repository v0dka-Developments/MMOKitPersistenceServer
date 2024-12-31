using Microsoft.AspNetCore.Mvc;
using System.Dynamic;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace PersistenceServer.Controllers
{
  
    public class PermissionValidator
    {
        public static bool HasPermission(int userPermissionLevel, TypeDefs.Permissions requiredPermission)
        {
            
            return (userPermissionLevel > (int)requiredPermission);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class AdminWebController : BaseMmoController
    {
        
        /*
         *
         *  login route
         *
         * 
         */
        [HttpPost("login")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> Login([FromBody] TypeDefs.LoginRequest request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            //check if cookie is present
            if (request.Cookie != null && request.Accountid != null && request.Charid != null && request.Permission != null)
            {
                Console.WriteLine("i made it here");
                int accountId = request.Accountid ?? 0; // methods dont allow non nulled ints...
                Console.WriteLine(accountId);
                if (MmoWsServer.Singleton!.CookieValidator.ValidateCookie(accountId, request.Cookie))
                {
                    Console.WriteLine("refreshing token");
                    var (cookiePart, ServerPart) = MmoWsServer.Singleton!.CookieValidator.RefreshToken(accountId);
                    // The cookie is valid
                    tcs.SetResult(Ok(new
                    {
                        title = "Token Refresh",
                        cookie = cookiePart,
                        accountid = request.Accountid,
                        charid = request.Charid,
                        permission = request.Permission,
                        status = 200,
                        traceId = HttpContext.TraceIdentifier
                    }));
                       
                }
                else
                {
                    Console.WriteLine("i do make it here for termination");
                    dynamic errors = new ExpandoObject();
                    errors.Username = new List<string> { "Token expired"};
                    tcs.SetResult(BadRequest(new
                    {
                        type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                        title = "Token expired",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));    
                }
                return await tcs.Task;
            }
            // Validate the request
            if (request.Username == null || request.Password == null || request.Username == "" || request.Password == "")
            {
                Console.WriteLine("i am working here?");
                dynamic errors = new ExpandoObject();
                errors.Request = request == null ? new List<string> { "Request body cannot be null." } : null;
                if (request?.Username == null) errors.Username = new List<string> { "The Username cannot be empty." };
                if (request?.Username == "") errors.Username = new List<string> { "The Username cannot be empty." };
                if (request?.Password == null) errors.Password = new List<string> { "The Password cannot be empty." };
                if (request?.Password == "") errors.Password = new List<string> { "The Password cannot be empty." };

                tcs.SetResult(BadRequest(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    title = "Validation errors occurred.",
                    status = 400,
                    errors,
                    traceId = HttpContext.TraceIdentifier
                }));    
                return await tcs.Task;
            }

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                var result = await MmoWsServer.Singleton!.Database.LoginWebUser(request.Username, request.Password);
                
                if (result == null)
                {
                    dynamic errors = new ExpandoObject();
                    errors.Credentials = new List<string> { "Null result for login request" };
                    // Handle login failure
                    tcs.SetResult(BadRequest(new
                    {
                        type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                        title = "Login failed.",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                }
                
                (int accountId, int charId, int permission) = result.Value;
                
                if (accountId >= 0)
                {
                    Console.WriteLine($"--------------------------\n " +
                                      $"| Admin Dashboard Login: |\n" +
                                      $"--------------------------\n" +
                                      $"user:  '{request.Username}' \n");
                                     
                    // Generate bcrypt hash and split into two parts
                    var ( cookiePart, ServerPart) = MmoWsServer.Singleton.CookieValidator.GenerateToken(accountId);
                   
                    // Send only the first part (client cookie)
                    dynamic response = new ExpandoObject();
                    response.Cookie = cookiePart;

                    tcs.SetResult(Ok(new
                    {
                        title = "Login Success",
                        cookie = cookiePart,
                        accountid = accountId,
                        charid = charId,
                        permission = permission,
                        status = 200,
                        traceId = HttpContext.TraceIdentifier
                    }));
                }
                else
                {
                    Console.WriteLine($"Login failed for '{request.Username}': bad credentials");
                    dynamic errors = new ExpandoObject();
                    errors.Credentials = new List<string> { "Invalid username or password." };
                    tcs.SetResult(BadRequest(new
                    {
                        type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                        title = "Login failed.",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                }
            });

            return await tcs.Task;
        }
        
        /*
         *
         *  every route request via the web interface gets run through this route validator
         *
         * 
         */
        
        
        [HttpPost("RouteValidator")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> RouteValidator([FromBody] TypeDefs.AuthRequest request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;
            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // check cookie is valid
                if (MmoWsServer.Singleton!.CookieValidator.ValidateCookie(accountId, request.Cookie))
                {
                    //validate the user has permissions
                    int result = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);

                    if (result == -1)
                    {
                        // -1 account doesnt exist so invalidate the cookie
                        MmoWsServer.Singleton!.CookieValidator.InvalidateToken(accountId);
                        
                        // The cookie is valid
                        tcs.SetResult(BadRequest(new
                        {
                            title = "Session Invalidated",
                            status = 400,
                            traceId = HttpContext.TraceIdentifier
                        }));
                        
                    }
                    else
                    {
                        if (result < 0 || result > 10)
                        {
                            // invalidate the token as permissions are invalid
                            MmoWsServer.Singleton!.CookieValidator.InvalidateToken(accountId);
                            tcs.SetResult(BadRequest(new
                            {
                                title = "Session Invalidated, incorrect permissions",
                                status = 400,
                                traceId = HttpContext.TraceIdentifier
                            }));
                        }
                        else
                        {
                            
                            // decide if we should randomly do a token refresh
                            Random random = new Random();
                            int check_token_refresh = random.Next(0, 100);
                            Console.WriteLine("random number is"+check_token_refresh);
                            if (check_token_refresh > 70)
                            {
                                // if it is higher than 70 then regen token
                                var (cookiePart, ServerPart) = MmoWsServer.Singleton!.CookieValidator.RefreshToken(accountId);
                                tcs.SetResult(Ok(new
                                {
                                    title = "Permission Check",
                                    cookie = cookiePart,
                                    accountid = request.Accountid,
                                    charid = request.Charid,
                                    permission = result,
                                    status = 200,
                                    traceId = HttpContext.TraceIdentifier
                                }));
                            }
                            else
                            {
                                tcs.SetResult(Ok(new
                                {
                                    title = "Permission Check",
                                    cookie = request.Cookie,
                                    accountid = request.Accountid,
                                    charid = request.Charid,
                                    permission = result,
                                    status = 200,
                                    traceId = HttpContext.TraceIdentifier
                                }));
                            }
                            
                           
                        }
                    }
                    
                }
                else
                {
                    MmoWsServer.Singleton!.CookieValidator.InvalidateToken(accountId);
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Session Invalidated",
                        status = 400,
                        traceId = HttpContext.TraceIdentifier
                    }));
                }
               
               
            });
            return await tcs.Task;
        }



        [HttpPost("FetchAccounts")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> FetchAccounts([FromBody] TypeDefs.AuthRequest request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                if (!MmoWsServer.Singleton!.CookieValidator.ValidateCookie(accountId, request.Cookie))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid Session" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);

                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchAccounts))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                // Fetch all accounts
                var accounts = await MmoWsServer.Singleton!.Database.allaccounts();
                
                tcs.SetResult(Ok(new
                {
                    title = "All Accounts",
                    data = accounts,
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        
        
        [HttpPost("FetchCharacters")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> FetchCharacters([FromBody] TypeDefs.RequestCharacters request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                Console.WriteLine(userPermissionLevel);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchCharacters))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                int accountid = request.FetchAccountId ?? 0;
                // Fetch all accounts
                var characters = await MmoWsServer.Singleton!.Database.usercharacters(accountid);
                
                tcs.SetResult(Ok(new
                {
                    title = "All Accounts",
                    data = characters,
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        
        [HttpPost("FetchWorldItemsList")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> FetchWorldItems([FromBody] TypeDefs.RequestCharacters request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                Console.WriteLine(userPermissionLevel);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchWorldItems))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                // Fetch all accounts
                var worlditems = await MmoWsServer.Singleton!.Database.worlditemspawnlist();
                
                tcs.SetResult(Ok(new
                {
                    title = "All World Items",
                    data = worlditems,
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        [HttpPost("FetchAllGuilds")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> FetchAllGuilds([FromBody] TypeDefs.RequestCharacters request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                Console.WriteLine(userPermissionLevel);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchGuilds))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                // Fetch all accounts
                var guilds = await MmoWsServer.Singleton!.Database.allguilds();
                
                tcs.SetResult(Ok(new
                {
                    title = "All World Items",
                    data = guilds,
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        
        [HttpPost("UpdateUserInventory")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> UpdateUserInventory([FromBody] TypeDefs.UpdateInventory request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;
            
            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                Console.WriteLine(userPermissionLevel);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchGuilds))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                var updateInventoryRequest = new TypeDefs.UpdateInventory
                {
                    selectedCharacterId = request.selectedCharacterId,
                    inventory = request.inventory 
                };
                var res =  await MmoWsServer.Singleton!.Database.UpdateInventory(request.selectedCharacterId, updateInventoryRequest);
                Console.WriteLine(res);
                tcs.SetResult(Ok(new
                {
                    title = "Inventory Updated",
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        
        
        [HttpPost("UpdateUserStats")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> UpdateUserStats([FromBody] TypeDefs.UpdateStats request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;
            
            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchGuilds))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                var updateStatsRequest = new TypeDefs.UpdateStats
                {
                    selectedCharacterId = request.selectedCharacterId,
                    stats = request.stats 
                };
                var res =  await MmoWsServer.Singleton!.Database.UpdateStats(request.selectedCharacterId, updateStatsRequest);
                Console.WriteLine(res);
                tcs.SetResult(Ok(new
                {
                    title = "Inventory Updated",
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        
        [HttpPost("UpdateUserAppearance")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> UpdateUserAppearance([FromBody] TypeDefs.UpdateAppearance request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;
            
            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchGuilds))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                var updateAppearanceRequest = new TypeDefs.UpdateAppearance
                {
                    selectedCharacterId = request.selectedCharacterId,
                    appearance = request.appearance 
                };
                var res =  await MmoWsServer.Singleton!.Database.UpdateAppearance(request.selectedCharacterId, updateAppearanceRequest);
                Console.WriteLine(res);
                tcs.SetResult(Ok(new
                {
                    title = "Inventory Updated",
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        
        [HttpPost("UpdateUserTransform")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> UpdateUserTransform([FromBody] TypeDefs.UpdateTransform request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;
            
            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchGuilds))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                var updateTransformRequest = new TypeDefs.UpdateTransform
                {
                    selectedCharacterId = request.selectedCharacterId,
                    transform = request.transform 
                };
                var res =  await MmoWsServer.Singleton!.Database.UpdateTransform(request.selectedCharacterId, updateTransformRequest);
                Console.WriteLine(res);
                tcs.SetResult(Ok(new
                {
                    title = "Inventory Updated",
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }
        
        [HttpPost("UpdateUserGuild")]
        [Authorize(Role.Anonymous)]
        public async Task<IActionResult> UpdateUserGuild([FromBody] TypeDefs.UpdateGuild request)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            int accountId = request.Accountid ?? 0;
            int charId = request.Charid ?? 0;
            
            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                // Validate the user's permissions
                int userPermissionLevel = await MmoWsServer.Singleton!.Database.validatepermissions(charId, accountId);
                if (!PermissionValidator.HasPermission(userPermissionLevel, TypeDefs.Permissions.FetchGuilds))
                {
                    dynamic errors = new ExpandoObject();
                    errors.Permissions = new List<string> { "Invalid permissions" };
                    tcs.SetResult(BadRequest(new
                    {
                        title = "Error",
                        status = 400,
                        errors,
                        traceId = HttpContext.TraceIdentifier
                    }));
                    return;
                }

                int guildid = request.guild ?? 0;
                int guildrank = request.guildRank ?? 0;
                    
                var res =  await MmoWsServer.Singleton!.Database.UpdateGuild(request.selectedCharacterId, guildid, guildrank);
                Console.WriteLine(res);
                tcs.SetResult(Ok(new
                {
                    title = "Inventory Updated",
                    status = 200,
                    traceId = HttpContext.TraceIdentifier
                }));
             
            });

            return await tcs.Task;
        }


        
        
        
        
        
        
        
        
        
    }
    
    
}
