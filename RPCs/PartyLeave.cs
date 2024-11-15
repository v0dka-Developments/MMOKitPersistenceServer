namespace PersistenceServer.RPCs
{
    internal class PartyLeave : BaseRpc
    {
        public PartyLeave()
        {
            RpcType = RpcType.RpcPartyLeave; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(connection));
        }

        private void ProcessMessage(UserConnection playerConn)
        {
            var player = Server!.GameLogic.GetPlayerByConnection(playerConn);
            if (player == null) return;

            if (player.PartyRef == null) return;
            player.PartyRef.RemoveMember(player, true);
        }
    }
}