namespace PersistenceServer.RPCs
{
    internal class PartyInvite : BaseRpc
    {
        public PartyInvite()
        {
            RpcType = RpcType.RpcPartyInvite; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string charName = reader.ReadMmoString();
            Server!.Processor.ConQ.Enqueue(() => ProcessMessage(charName, connection));
        }

        // 0. check if sender has authority to invite to a party (he's either party leader or without a party)
        // 1. check if target player is online
        // 2. check if target player is partyless
        // 3. check if target player isn't sender himself
        // 4. check if target player has a pending invite
        // 5. send the invite to player if all is clear
        // 6. send confirmation to sender if all is clear
        // N.B. When sending RpcPartyInvite back to clients, the first byte (a bool) is true when sending to Invite Recipient, and false when sending back to Invite Sender
        private void ProcessMessage(string recipientName, UserConnection senderConn)
        {
            var sender = Server!.GameLogic.GetPlayerByConnection(senderConn);
            if (sender == null) return; // player could've disconnected in the meantime, so we abort

            if (sender.PartyRef != null)
            {                
                if (sender.PartyRef.PartyLeaderId != sender.CharId)
                {
                    // no authority to invite, you're not the party leader
                    Console.WriteLine($"{DateTime.Now:HH:mm} Party Invite from {sender.Name} to {recipientName} failed: player no authority to invite");
                    byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcPartyInvite), ToBytes(false), ToBytes(0)); // 0 is a message that says "you have no authority"
                    senderConn.Send(msgFail);
                    return;
                }

                // can't invite, party is full
                if (sender.PartyRef.IsPartyFull())
                {
                    // can't invite, you're at max 
                    Console.WriteLine($"{DateTime.Now:HH:mm} Party Invite from {sender.Name} to {recipientName} failed: party full");
                    byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcPartyInvite), ToBytes(false), ToBytes(1)); // 1 is a message that says "party full"
                    senderConn.Send(msgFail);
                    return;
                }
            }

            // attempt to find recipient online
            var recipient = Server!.GameLogic.GetPlayerByName(recipientName);
            if (recipient == null)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Party Invite from {sender.Name} to {recipientName} failed: no player found");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcNoSuchPlayer)); // send "no such player"
                senderConn.Send(msgFail);
                return;
            }

            // can't invite self to party
            if (recipient == sender)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Party Invite from {sender.Name} to {recipientName} failed: can't invite self");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcNoSuchPlayer)); // send "no such player"
                senderConn.Send(msgFail);
                return;
            }

            // check if recipient is partyless
            if (recipient.PartyRef != null)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Party Invite from {sender.Name} to {recipientName} failed: player is already in party");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcPartyInvite), ToBytes(false), ToBytes(2)); // 2 is a message that says "player is already in party"
                senderConn.Send(msgFail);
                return;
            }

            // check if recipient isn't busy (has a pending invite)
            if (recipient.HasPendingInvite())
            {
                Console.WriteLine($"{DateTime.Now:HH:mm} Party Invite from {sender.Name} to {recipientName} failed: player has a pending invite");
                byte[] msgFail = MergeByteArrays(ToBytes(RpcType.RpcPartyInvite), ToBytes(false), ToBytes(3)); // 3 is a message that says "player is busy"
                senderConn.Send(msgFail);
                return;
            }

            Console.WriteLine($"{DateTime.Now:HH:mm} {sender.Name} invites {recipientName} to party.");
            recipient.SetInviteToParty(sender.Name, sender.PartyId);

            byte[] msgToInvitee = MergeByteArrays(ToBytes(RpcType.RpcPartyInvite), ToBytes(true), WriteMmoString(sender.Name));
            recipient.Conn.Send(msgToInvitee);            

            byte[] msgToSender = MergeByteArrays(ToBytes(RpcType.RpcPartyInvite), ToBytes(false), ToBytes(4)); // 4 is a message that says "invite sent successfully"
            senderConn.Send(msgToSender);
        }
    }
}