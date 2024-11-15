using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer
{
    public enum Role
    {
        Client,
        Server,
        Anonymous
    }

    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly HashSet<Role> _allowedRoles;

        public AuthorizeAttribute(params Role[] allowedRoles)
        {
            _allowedRoles = new HashSet<Role>(allowedRoles);
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var Headers = context.HttpContext.Request.Headers;

            // if we have Server headers, check that we have a Server role and validate server password
            if (Headers.TryGetValue("ServerPassword", out StringValues ServerPassword) && Headers.TryGetValue("ServerGuid", out StringValues Guid))
            {
                if (_allowedRoles.Contains(Role.Server))
                {
                    if (MmoWsServer.Singleton!.Settings.ServerPassword == ServerPassword)
                    {
                        context.HttpContext.Items["ServerGuid"] = Guid.ToString();
                        context.HttpContext.Items["Role"] = Role.Server;
                        return;
                    }
                }
                ForbidResult(context);
                return;
            }

            // if we have Client headers, check that we have a Client role and validate headers
            if (Headers.TryGetValue("Cookie", out StringValues Cookie) && Headers.TryGetValue("CharId", out StringValues CharId))
            {
                if (_allowedRoles.Contains(Role.Client))
                {
                    context.HttpContext.Items["Cookie"] = Cookie.ToString();
                    if (Headers.TryGetValue("Account", out StringValues AccountId)) // optional, for PIE & Debug only
                    {
                        if (int.TryParse(AccountId.ToString(), out int accountId))
                            context.HttpContext.Items["Account"] = accountId;
                    }
                    if (int.TryParse(CharId.ToString(), out int charId))
                    {
                        context.HttpContext.Items["CharId"] = charId;
                        context.HttpContext.Items["Role"] = Role.Client;
                        return;
                    }
                }
                ForbidResult(context);
                return;
            }

            // if no Client and Server headers are present, but Anonymous role is allowed, authorize
            if (_allowedRoles.Contains(Role.Anonymous))
            {
                context.HttpContext.Items["Role"] = Role.Anonymous;
                return;
            }

            ForbidResult(context);
        }

        // Normally we could just do context.Result = new ForbidResult();
        // But since UE5 currently can't read status code, we return a custom JSON body with the status code
        void ForbidResult(AuthorizationFilterContext context)
        {
            context.Result = new ObjectResult(new { error = "Access Denied: Role not authorized", status = 403 })
            {
                StatusCode = 403
            };
        }
    }
}
