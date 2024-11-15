using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OnlineStatsController : BaseMmoController
    {
        // GET: api/onlinestats/players
        // In browser: http://127.0.0.1:3457/api/onlinestats/players
        [HttpGet("players")]
        [Authorize(Role.Client, Role.Server, Role.Anonymous)]
        public Task<IActionResult> GetPlayersOnline()
        {
            var tcs = new TaskCompletionSource<IActionResult>();

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {                
                dynamic response = new ExpandoObject();
                response.Players = MmoWsServer.Singleton!.GameLogic.GetPlayersOnline();
                tcs.SetResult(Ok(response));
            });

            return tcs.Task;
        }
    }
}
