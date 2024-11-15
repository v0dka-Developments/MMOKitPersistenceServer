namespace PersistenceServer.RPCs
{
    public class LoginClientWithCookie : BaseRpc
    {
        public LoginClientWithCookie()
        {
            RpcType = RpcType.RpcLoginClientWithCookie; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            var cookie = reader.ReadMmoString();
            var charId = reader.ReadInt32();
#if DEBUG
            if (Server!.Settings.UniversalCookie == cookie)
            {
                Server!.Processor.ConQ.Enqueue(async () => await ProcessLoginClientFromEditor(charId, connection));
            }
            else 
            {
                Server!.Processor.ConQ.Enqueue(async () => await ProcessLoginClientWithCookie(cookie, charId, connection));
            }
#else
            Server!.Processor.ConQ.Enqueue(async () => await ProcessLoginClientWithCookie(cookie, charId, connection));
#endif
        }

        private async Task ProcessLoginClientWithCookie(string cookie, int charId, UserConnection connection)
        {
            var accountId = Server!.GameLogic.GetAccountIdByCookie(cookie);
            if (accountId < 0)
            {
                Console.WriteLine("LoginWithCookie failed for client: bad cookie");
                connection.Disconnect();
                return;
            }
            
            var charname = await Server!.Database.DoesAccountOwnCharacter(accountId, charId);
            if (charname != null)
            {
                Console.WriteLine($"Client relogged with character: {charname}");
                Server!.GameLogic.UserReconnected(connection, accountId, charId, charname);
            }
            else
            {
                Console.WriteLine("LoginWithCookie failed for client: bad char id");
                connection.Disconnect();
            }
        }

        // If we're in Debug config, we assume the client connects from PIE and therefore doesn't have a valid cookie
        // Neither does it have a valid charId. It'll provide Pie Window ID instead, couting from 0.
        private async Task ProcessLoginClientFromEditor(int pieWindowId, UserConnection connection)
        {
            var result = await Server!.Database.GetCharacterForPieWindow(pieWindowId);
            if (result == null)
            {
                Console.WriteLine($"LoginWithCookie failed for client: not enough characters in DB for PIE window: {pieWindowId}");
                connection.Disconnect();
                return;
            }
            Server!.GameLogic.UserReconnected(connection, result.Item4, result.Item1, result.Item2);
        }
    }
}