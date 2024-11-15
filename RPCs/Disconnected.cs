using Newtonsoft.Json;
using System;
using System.Threading.Channels;

namespace PersistenceServer.RPCs
{
    public class Disconnected : BaseRpc
    {
        public Disconnected()
        {
            RpcType = RpcType.RpcDisconnected; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
#if DEBUG
            Console.WriteLine($"(thread {Environment.CurrentManagedThreadId}) Client Disconnected");
#endif
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(connection));
        }

        private void ProcessMessage(UserConnection conn)
        {
            var player = Server!.GameLogic.GetPlayerByConnection(conn);

            if (player != null)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} {player.Name} went offline.");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} User disconnected. Session Id: {conn.Id}");
                Server!.GameLogic.UserDisconnected(conn);
                return;
            }

            var guild = Server!.GameLogic.GetPlayerGuild(conn);
            if (guild != null && player != null)
            {
                // send a message to other online guild members that this one just went offline
                // for client, params are: char id, guild rank, online status (bool)
                byte[] msgToGuildies = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(player.CharId), ToBytes((int)player.GuildRank!), ToBytes(false)); // true for online
                foreach (var onlineMember in guild.GetOnlineMembers())
                {
                    // if this is our character, we don't need to tell him that he just went offline
                    if (onlineMember.Conn == conn) continue;
                    onlineMember.Conn.Send(msgToGuildies);
                }
            }

            var charId = player!.CharId;
            var party = player.PartyRef;

            Server!.GameLogic.UserDisconnected(conn);
            
            if (party != null)
            {
                Server!.GameLogic.StoreDisconnectedPlayerPartyId(charId, party.Id);

                // if party still has online members send a new full party info to the remaining players
                if (party.HasOnlineMembers())
                {
                    party.SendFullPartyToInvolvedServers();
                }
                // if party has no currently online members, disband the party
                else
                {
                    // this will send the info to servers too
                    // party.DisbandParty();
                }                
            }
        }
    }
}