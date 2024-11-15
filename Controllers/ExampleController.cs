using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

// Remove various warnings, since it's an example file.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

/*
 * Useful methods to call inside functions: GetRole(), GetGameServer(), GetPlayer()
 * To print a dynamic object: DynamicHelper.ToString(exampleData)
 */
namespace PersistenceServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExampleController : BaseMmoController
    {
        // Post Example 1:
        // This examples receives a body and reads its JSON into a dynamic object
        [HttpPost("create1")] // POST: api/example/create1
        [Authorize(Role.Client, Role.Server, Role.Anonymous)]
        public async Task<IActionResult> CreateExample1()
        {            
            var tcs = new TaskCompletionSource<IActionResult>();

            dynamic? exampleData = await GetBodyJsonAsync();
            if (exampleData == null)
            {
                tcs.SetResult(BadRequest());
                return await tcs.Task;
            }
            Console.WriteLine(DynamicHelper.ToString(exampleData));

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                Console.WriteLine($"Awesome. Example char name: {exampleData.charName}");
                tcs.SetResult(Ok(exampleData));
            });

            return await tcs.Task;
        }

        // Post Example 2:
        // This examples receives a body and reads its JSON into an object of a C# class (as a function's parameter)
        [HttpPost("create2")] // POST: api/example/create2
        [Authorize(Role.Client, Role.Server, Role.Anonymous)]
        public async Task<IActionResult> CreateExample2([FromBody] ExampleData exampleData)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            if (exampleData == null)
            {
                tcs.SetResult(BadRequest());
                return await tcs.Task;
            }

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                Console.WriteLine("awesome");
                tcs.SetResult(Ok(exampleData));
            });

            return await tcs.Task;
        }

        // Get Example
        // This example explores diffrent ways of responding to a request
        [HttpGet("{id:int}")] // GET: api/example/{id}
        [Authorize(Role.Client, Role.Server, Role.Anonymous)]
        public Task<IActionResult> GetExample(int id)
        {
            var tcs = new TaskCompletionSource<IActionResult>();
            Role validatedRole = GetRole();            

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () => {
                //await Task.Delay(2000); // 2000 milliseconds = 2 seconds
                //var test = await MmoWsServer.Singleton!.Database.GetCharacter(4, 1);

                // working example 1: data in an object of a specific C# class
                //var response = new ExampleData();
                //response.Id = 12;
                //response.Name = "tert";
                //tcs.SetResult(Ok(response));

                // working example 2: anonymous object
                //var response = new
                //{
                //    Id = 1,
                //    MemberName = "test",
                //    GuildRank = 2,
                //    Online = false
                //};
                //tcs.SetResult(Ok(response));

                // working example 3: dynamic object
                dynamic response = new ExpandoObject();
                response.Id = 1;
                response.MemberName = "test 3";
                response.GuildRank = 2;
                response.Online = false;
                tcs.SetResult(Ok(response));

                // working example 4: just a string
                //var response = "Example";
                //tcs.SetResult(Ok(response));

                // working example 5: rejecting with status code
                // tcs.SetResult(BadRequest());
                // Other potentially useful status codes:
                // BadRequest() returns json with status code 400
                // Unauthorized() returns json with status code 401
                // NotFound() returns json with status code 404                                
                // Conflict() returns json with status code 409               
            });

            return tcs.Task;
        }
    }

    public class ExampleData
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.