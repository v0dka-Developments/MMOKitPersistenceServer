namespace PersistenceServer.RPCs
{
    internal class PartyDisband : BaseRpc
    {
        public PartyDisband()
        {
            RpcType = RpcType.RpcPartyDisband; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(connection));
        }

        private void ProcessMessage(UserConnection connection)
        {
            var player = Server!.GameLogic.GetPlayerByConnection(connection);
            if (player == null) return;
            if (player.PartyRef == null) return;

            // don't have the rights
            if (player.PartyRef.PartyLeaderId != player.CharId)
            {
                byte[] msgYourNotLeader = MergeByteArrays(ToBytes(RpcType.RpcPartyChangeLeader), ToBytes(false)); // RpcPartyChangeLeader + false is "You're not the party leader" message
                connection.Send(msgYourNotLeader);
                return;
            }

            player.PartyRef.DisbandParty(true);
        }
    }
}