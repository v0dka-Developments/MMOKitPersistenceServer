using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PersistenceServer.Controllers;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer
{
    public class BaseMmoController : ControllerBase
    {
        protected Role GetRole()
        {
            return (Role)HttpContext.Items["Role"]!;
        }

        protected Player? GetPlayer()
        {
            string clientCookie = (string)HttpContext.Items["Cookie"]!;
            var charId = (int)HttpContext.Items["CharId"]!;

            // In PIE, it's a special case
            // Because the login works through a universal cookie, PS doesn't keep a map of <cookie, account>, so in Debug the PS will
            // trust your supplied Account ID instead of getting it from cookie. But we still check for the validity of the universal cookie.
            int accountId = -1;
#if DEBUG
            if (clientCookie == MmoWsServer.Singleton!.Settings.UniversalCookie && HttpContext.Items.ContainsKey("Account"))
                accountId = (int)HttpContext.Items["Account"]!;         
            else
                accountId = MmoWsServer.Singleton!.GameLogic.GetAccountIdByCookie(clientCookie);
#else
            accountId = MmoWsServer.Singleton!.GameLogic.GetAccountIdByCookie(clientCookie);
#endif
            Player? player = MmoWsServer.Singleton!.GameLogic.GetPlayerById(charId);

            // We get account id from cookie, which can't be guessed by other players
            // Then we retreive the character by char id that the client supplied (which can be faked)
            // If the retrieved character belongs to the same account, as the cookie belongs to, the validity check is considered passed and we return the Player
            if (player != null && player.AccountId == accountId)
                return player;
            else
                return null;
        }

        protected GameServer? GetGameServer()
        {
            string serverGuid = (string)HttpContext.Items["ServerGuid"]!;
            return MmoWsServer.Singleton!.GameLogic.GetServerByGuid(serverGuid);
        }

        protected async Task<dynamic?> GetBodyJsonAsync()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<ExpandoObject>(body);
        }
    }

    public static class DynamicHelper
    {
        public static string ToString(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
    }
}
