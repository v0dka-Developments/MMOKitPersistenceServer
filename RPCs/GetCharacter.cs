namespace PersistenceServer.RPCs
{
    public class GetCharacter : BaseRpc
    {
        public GetCharacter()
        {
            RpcType = RpcType.RpcGetCharacter; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string cookie = reader.ReadMmoString();
            int charId = reader.ReadInt32();
#if DEBUG
            if (Server!.Settings.UniversalCookie == cookie)
            {
                Server!.Processor.ConQ.Enqueue(async () => await ProcessGetCharacterForPie(charId, connection));
            }
            else
            {
                Server!.Processor.ConQ.Enqueue(async () => await ProcessGetCharacter(cookie, charId, connection));
            }
#else
            Server!.Processor.ConQ.Enqueue(async () => await ProcessGetCharacter(cookie, charId, connection));
#endif
        }

        private async Task ProcessGetCharacter(string cookie, int charId, UserConnection connection)
        {
            if (!Server!.GameLogic.IsServer(connection))
            {
                Console.WriteLine("Illegal action: not a server tried GetCharacter RPC. This must never happen: investigate if it does.");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcGetCharacter), ToBytes(false));
                connection.Send(msg);
                return;
            }

            var accountId = Server!.GameLogic.GetAccountIdByCookie(cookie);
            if (accountId < 0)
            {
                Console.WriteLine("GetCharacter failed: user provided bad cookie!");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcGetCharacter), ToBytes(false)); // this will tell the game server to disconnect this user
                connection.Send(msg);
                return;
            }

            var character = await Server!.Database.GetCharacter(charId, accountId);
            if (character == null)
            {
                Console.WriteLine("GetCharacter failed: user provided bad char id!");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcGetCharacter), ToBytes(false)); // this will tell the game server to disconnect this user
                connection.Send(msg);
                return;
            }

            //@TODO: have a bool for allowMultipleCharacters and if it's false or if we're in DEBUG, then tell the game servers to disconnect the oldest character from account ID

            byte[] binAccountId = ToBytes(accountId);
            byte[] binCharId = ToBytes(charId);
            byte[] binCharname = WriteMmoString(character.Item1);
            byte[] binSerialized = WriteMmoString(character.Item2);
            byte[] binGuild = ToBytes(character.Item3 ?? -1);
            byte[] binGuildrank = ToBytes(character.Item4 ?? -1);
            Console.WriteLine($"GetCharacter processed for: {character.Item1}");
            byte[] msgSuccess = MergeByteArrays(ToBytes(RpcType.RpcGetCharacter), ToBytes(true), binAccountId, binCharId, binCharname, binSerialized, binGuild, binGuildrank);
            connection.Send(msgSuccess);
        }

        private async Task ProcessGetCharacterForPie(int pieWindowId, UserConnection connection)
        {
            var character = await Server!.Database.GetCharacterForPieWindow(pieWindowId);
            if (character == null)
            {
                Console.WriteLine($"LoginWithCookie failed for client: not enough characters in DB for PIE window: {pieWindowId}");
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcGetCharacter), ToBytes(false)); // this will tell the game server to disconnect this user
                connection.Send(msg);
                return;
            }
            byte[] binAccountId = ToBytes(character.Item4);
            byte[] binCharId = ToBytes(character.Item1);
            byte[] binCharname = WriteMmoString(character.Item2);
            byte[] binSerialized = WriteMmoString(character.Item3);
            byte[] binGuild = ToBytes(character.Item5 ?? -1);
            byte[] binGuildrank = ToBytes(character.Item5 ?? -1);
            Console.WriteLine($"GetCharacter processed for: {character.Item2}");
            byte[] msgSuccess = MergeByteArrays(ToBytes(RpcType.RpcGetCharacter), ToBytes(true), binAccountId, binCharId, binCharname, binSerialized, binGuild, binGuildrank);
            connection.Send(msgSuccess);
        }
    }
}