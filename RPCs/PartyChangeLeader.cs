namespace PersistenceServer.RPCs
{
    internal class PartyChangeLeader : BaseRpc
    {
        public PartyChangeLeader()
        {
            RpcType = RpcType.RpcPartyChangeLeader; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            int newLeaderId = reader.ReadInt32();
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(newLeaderId, connection));
        }

        private void ProcessMessage(int newLeaderId, UserConnection connection)
        {
            var player = Server!.GameLogic.GetPlayerByConnection(connection);
            if (player == null) return;
            if (player.PartyRef == null) return;

            // don't have the rights
            if (player.PartyRef.PartyLeaderId != player.CharId)
            {
                byte[] msgYourNotLeader = MergeByteArrays(ToBytes(RpcType.RpcPartyChangeLeader), ToBytes(false)); // false is "You're not the party leader" message
                connection.Send(msgYourNotLeader);
                return; 
            }

            // don't have such player in party
            if (!player.PartyRef.MemberIds.Contains(newLeaderId)) return;

            // if all checks are valid            
            player.PartyRef.PartyLeaderId = newLeaderId;
            player.PartyRef.SendFullPartyToInvolvedServers();

            string newLeaderName = player.PartyRef.GetMemberName(newLeaderId);
            byte[] msgCharRankAdjusted = MergeByteArrays(ToBytes(RpcType.RpcPartyChangeLeader), ToBytes(true), WriteMmoString(newLeaderName));
            foreach (var conn in player.PartyRef.GetClientConnections())
            {
                conn.Send(msgCharRankAdjusted);
            }
        }
    }
}