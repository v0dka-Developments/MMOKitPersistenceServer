namespace PersistenceServer.RPCs
{
    internal class MessageParty : BaseRpc
    {
        public MessageParty()
        {
            RpcType = RpcType.RpcMessageParty; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string message = reader.ReadMmoString();
            int maxLength = 255;
            // Trim message by maxLength (255 characters)
            message = message.Length <= maxLength ? message : message[..maxLength]; // .. is a C# 8.0 Range Operator https://www.codeguru.com/csharp/c-8-0-ranges-and-indices-types/
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(message, connection));
        }

        private void ProcessMessage(string message, UserConnection connection)
        {
            var player = Server!.GameLogic.GetPlayerByConnection(connection);
            if (player == null) return;

            if (player.PartyRef == null)
            {
                Console.WriteLine($"Player {player.Name} attempted a party message, but is not in a party");
                return;
            }

            bool partyLeader = player.PartyRef.PartyLeaderId == player.CharId;

            Console.WriteLine($"{DateTime.Now:HH:mm} [{(partyLeader ? "Party Leader" : "Party")} ({player.PartyId})] {player.Name}: \"{message}\"");
            // Channel 8 is Party channel, see EChatMsgChannel in ue5
            // Channel 9 is Party Leader channel
            int channel = partyLeader ? 9 : 8;
            byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcMessageChannel), ToBytes(channel), WriteMmoString(player.Name), WriteMmoString(message), ToBytes(false) /*not a GM message*/);
            foreach (var partyMemberConn in player.PartyRef.GetClientConnections())
            {
                partyMemberConn.Send(msg);
            }
        }
    }
}