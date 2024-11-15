namespace PersistenceServer.RPCs
{
    internal class PartyKick : BaseRpc
    {
        public PartyKick()
        {
            RpcType = RpcType.RpcPartyKick; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            bool kickByName = reader.ReadBoolean();
            if (kickByName)
            {
                string kickedName = reader.ReadMmoString();
                Server!.Processor.ConQ.Enqueue(() => ProcessKickByName(kickedName, connection));
            }
            else
            {
                int kickedCharId = reader.ReadInt32();
                Server!.Processor.ConQ.Enqueue(() => ProcessKickById(kickedCharId, connection));
            }
        }

        private void ProcessKickByName(string kickedName, UserConnection connection)
        {
            var player = Server!.GameLogic.GetPlayerByName(kickedName);
            if (player == null) return;
            ProcessKickById(player.CharId, connection);            
        }

        private void ProcessKickById(int kickedCharId, UserConnection connection)
        {
            var player = Server!.GameLogic.GetPlayerByConnection(connection);
            if (player == null) return;
            if (player.PartyRef == null) return;

            // don't have such player in party
            if (!player.PartyRef.MemberIds.Contains(kickedCharId)) return;

            // don't have the rights
            if (player.PartyRef.PartyLeaderId != player.CharId)
            {
                byte[] msgYourNotLeader = MergeByteArrays(ToBytes(RpcType.RpcPartyChangeLeader), ToBytes(false)); // RpcPartyChangeLeader + false is "You're not the party leader" message
                connection.Send(msgYourNotLeader);
                return;
            }

            player.PartyRef.RemoveMemberById(kickedCharId, false);
        }
    }
}