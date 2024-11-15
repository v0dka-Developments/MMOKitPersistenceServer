using Microsoft.AspNetCore.Mvc;
using PersistenceServer.RPCs;
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
    public class PollPartyMemberController : BaseMmoController
    {
        [HttpGet("{id:int}")] // GET api/PollPartyMember/{id}
        [Authorize(Role.Client)] // authorize logged-in clients only
        public Task<IActionResult> GetBar(int id)
        {
            var tcs = new TaskCompletionSource<IActionResult>();

            MmoWsServer.Singleton!.Processor.ConQ.Enqueue(async () =>
            {
                Player? player = GetPlayer();
                if (player == null || player.PartyRef == null)
                {
                    tcs.SetResult(NotFound());
                    return;
                }
                var memberStats = player.PartyRef.GetMemberStats(id);
                if (memberStats == null)
                {
                    tcs.SetResult(NotFound());
                    return;
                }
                PartySyncMessageMember response = new PartySyncMessageMember(id, memberStats.CurHp, memberStats.MaxHp);
                tcs.SetResult(Ok(response));
            });

            return tcs.Task;
        }
    }
}
