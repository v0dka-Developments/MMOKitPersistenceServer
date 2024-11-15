using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer
{
    public class Player
    {
        public string Name;
        public UserConnection Conn;
        public int CharId;
        public int AccountId;
        public int GuildId; // -1 for guildless
        public int GuildRank; // -1 for guildless
        public int Permissions; // 0 for player, 10 for GM, but this can be changed in Server!.Database.CreateCharacter
        public Party? PartyRef;
        public string PartyId { get { return PartyRef != null ? PartyRef.Id : ""; } }

        private PendingInvitation? invite = null;

        public Player(UserConnection conn, DatabaseCharacterInfo dbPlayer)
        {
            Name = dbPlayer.Name;
            Conn = conn;
            AccountId = dbPlayer.AccountId;
            CharId = dbPlayer.CharId;
            GuildId = dbPlayer.Guild ?? -1;
            GuildRank = dbPlayer.GuildRank ?? -1;
            Permissions = dbPlayer.Permissions;
        }

        public bool IsGm()
        {
            return Permissions > 0;
        }

        // if last invite is valid and was issued less than 30 seconds ago, player has a pending invite
        public bool HasPendingInvite()
        {
            return invite != null && (DateTime.Now - invite.timeInvited).TotalSeconds < 30;
        }

        public PendingInvitation? GetPendingInvite()
        {
            return invite;
        }

        public void SetInviteToGuild(string inviterName, int guildId)
        {
            invite = new GuildInvitation(inviterName, guildId);
        }

        public void SetInviteToParty(string inviterName, string partyId)
        {
            invite = new PartyInvitation(inviterName, partyId);
        }

        public void ClearPendingInvite()
        {
            invite = default;
        }
    }

    public abstract class PendingInvitation
    {
        public DateTime timeInvited = default; // default is 1/1/0001 12:00:00 AM
        public string inviterName = "";
    }

    public class GuildInvitation : PendingInvitation
    {
        public int guildId;
        public GuildInvitation(string inInviter, int inGuildId)
        {
            timeInvited = DateTime.Now;
            inviterName = inInviter;
            guildId = inGuildId;
        }
    }

    public class PartyInvitation : PendingInvitation
    {
        public string partyId; // party id is an empty string if the inviter isn't in a party yet
        public PartyInvitation(string inInviter, string inPartyId) {
            timeInvited = DateTime.Now;
            inviterName = inInviter;
            partyId = inPartyId;
        }
    }
}
