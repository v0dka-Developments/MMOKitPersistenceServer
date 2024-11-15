using System;
using System.Numerics;

namespace PersistenceServer.RPCs
{
    internal class AcceptInvite : BaseRpc
    {
        public AcceptInvite()
        {
            RpcType = RpcType.RpcAcceptInvite; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            bool accept = reader.ReadBoolean();
            Server!.Processor.ConQ.Enqueue(async () => await ProcessMessage(accept, connection));
        }

        private async Task ProcessMessage(bool accept, UserConnection senderConn)
        {
            var invitedPlayer = Server!.GameLogic.GetPlayerByConnection(senderConn);
            if (invitedPlayer == null) return;
            // if timeout happened
            if (!invitedPlayer.HasPendingInvite())
            {
                byte responseByte = 3;
                byte[] msgExpired = MergeByteArrays(ToBytes(RpcType.RpcAcceptInvite), responseByte); // response byte 3 means "invitation has expired"
                senderConn.Send(msgExpired);
                return; 
            }

            PendingInvitation invite = invitedPlayer.GetPendingInvite()!;

            var inviter = Server!.GameLogic.GetPlayerByName(invite.inviterName);
            if (inviter != null) // inviter may have disconnected in the meantime
            {
                // this is where we tell the inviter that the player accepted or rejected the invitation
                byte[] msgToInviter = MergeByteArrays(ToBytes(RpcType.RpcAcceptInvite), ToBytes(accept), WriteMmoString(invitedPlayer.Name));
                inviter.Conn.Send(msgToInviter);
            }

            if (accept && invite is GuildInvitation guildInvite)
            {
                var guild = Server!.GameLogic.GetGuildById(guildInvite.guildId);

                // if guild got disbanded in the meantime, just return
                // or if player isn't guildless, return (e.g. joined another guild in the meantime by creating one)
                if (guild == null || invitedPlayer.GuildId != -1)
                {
                    invitedPlayer.ClearPendingInvite();
                    return;
                }

                Console.WriteLine($"{DateTime.Now:HH:mm} {invitedPlayer.Name} joins the guild {guild.Name}");

                int rank = Server.Settings.DefaultGuildRank;                
                await Server!.Database.AddGuildMember(guildInvite.guildId, invitedPlayer.CharId, rank);
                Server!.GameLogic.AddGuildMember(guild, invitedPlayer, rank);

                // send the newly created guild roster to all the members
                byte[] msgNewRoster = MergeByteArrays(ToBytes(RpcType.RpcGuildAllMembersUpdate), WriteMmoString(guild.GetGuildMembersJson()));
                byte[] msgPlayerJoined = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberJoined), WriteMmoString(invitedPlayer.Name));
                foreach (var member in guild.GetOnlineMembers())
                {
                    member.Conn.Send(msgNewRoster);
                    // send "player joined your guild" to all online members except to the joining player
                    if (member != invitedPlayer)
                    {
                        member.Conn.Send(msgPlayerJoined);
                    }
                }

                // send message to all servers that a character with a certain id belongs to a guild with a certain id & name
                // For server, params are: char id, guild name, guild id, guild rank
                byte[] msgToServers = MergeByteArrays(ToBytes(RpcType.RpcGuildMemberUpdate), ToBytes(invitedPlayer.CharId), WriteMmoString(guild.Name), ToBytes(guild.Id), ToBytes(invitedPlayer.GuildRank));
                foreach (var serverConn in Server!.GameLogic.GetAllServerConnections())
                {
                    serverConn.Send(msgToServers);
                }
            }

            // if inviter is partyless, we need to create a party
            // if inviter is in a party, the new member needs to join (and check that we're not exceeding full party)
            // there's also some checks to make if inviter has himself joined a different party in the meantime, or switched a party, then we shouldn't be able to accept the invite
            // but if the inviter wasn't a party leader before, but now is, we must still process the invite
            // inviter may also be null if he disconnected,... do we accept the invite then or what?
            if (accept && invite is PartyInvitation partyInvite)
            {
                // if the inviter has gone offline, don't party up
                if (inviter == null)
                {
                    byte responseByte = 4;
                    byte[] msgExpired = MergeByteArrays(ToBytes(RpcType.RpcAcceptInvite), responseByte); // response byte 4 means "the player has gone offline"
                    senderConn.Send(msgExpired);
                    invitedPlayer.ClearPendingInvite();
                    return;
                }

                // if the invitation was to an existing party
                if (partyInvite.partyId != "")
                {
                    // if the party still exists and isn't full, join with this party
                    var party = Server!.GameLogic.GetPartyById(partyInvite.partyId);
                    if (party != null)
                    {
                        if (!party.IsPartyFull())
                        {
                            party.AddMember(invitedPlayer); // this takes care of sending all the necessary messages
                        }
                        // if party is full, send "party is full" to invitedPlayer
                        else
                        {
                            byte responseByte = 2;
                            byte[] msgPartyFull = MergeByteArrays(ToBytes(RpcType.RpcAcceptInvite), responseByte); // response byte 2 means "party is full"
                            invitedPlayer.Conn.Send(msgPartyFull);
                        }
                    } 
                    // if the party no longer exists, but the inviter is partyless, create a party
                    else if (inviter != null && inviter.PartyRef == null)
                    {
                        new Party(inviter, invitedPlayer); // this takes care of sending all the necessary messages
                    }
                }
                // if the invitation was to an empty party
                else
                {
                    // but if now the inviter is in a party, make sure he's the leader. if he's not, we can't join.
                    if (inviter.PartyRef is Party party)
                    {
                        if (party.PartyLeaderId == inviter.CharId)
                        {
                            party.AddMember(invitedPlayer); // this takes care of sending all the necessary messages
                        }
                        else
                        {
                            byte responseByte = 3;
                            byte[] msgExpired = MergeByteArrays(ToBytes(RpcType.RpcAcceptInvite), responseByte); // response byte 3 means "invitation has expired"
                            senderConn.Send(msgExpired);
                        }
                    }
                    // if the inviter is still partyless, create the party
                    else
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm} Party created");
                        new Party(inviter, invitedPlayer); // this takes care of sending all the necessary messages
                    }                    
                }
            }

            invitedPlayer.ClearPendingInvite();
        }
    }
}