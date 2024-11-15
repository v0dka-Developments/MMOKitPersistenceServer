using Newtonsoft.Json;

namespace PersistenceServer.RPCs
{
    internal class PartyMembersSync : BaseRpc
    {
        public PartyMembersSync()
        {
            RpcType = RpcType.RpcPartyMembersSync; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            string jsonString = reader.ReadMmoString();
            var syncMessage = JsonConvert.DeserializeObject<PartySyncMessageFull>(jsonString);
            if (syncMessage == null) return;
            if (syncMessage.Parties.Length > 0)
            {
                Server!.Processor.ConQ.Enqueue(() => ProcessPartyMembersSync(syncMessage, connection));
            }
        }

        private void ProcessPartyMembersSync(PartySyncMessageFull syncMessage, UserConnection connection)
        {            
            foreach (var partyInfo in syncMessage.Parties)
            {
                if (partyInfo.Members.Length > 0)
                {
                    var party = Server!.GameLogic.GetPartyById(partyInfo.PartyId);
                    if (party == null) continue;
                    foreach (var member in partyInfo.Members)
                    {
                        // Console.WriteLine($"Updated info for {party.GetMemberName(member.CharId)}: {member.CurHp}/{member.MaxHp} hp");
                        party.UpdateMemberStats(member.CharId, member.CurHp, member.MaxHp);
                    }
                }
            }
        }
    }

    /* Corresponds to FPartySyncFull */
    public class PartySyncMessageFull
    {
        [JsonProperty]
        public required PartySyncMessageParty[] Parties { get; set; }
    }

    /* Corresponds to FPartySyncOneParty */
    public class PartySyncMessageParty
    {
        [JsonProperty]
        public required string PartyId { get; set; }
        [JsonProperty]
        public required PartySyncMessageMember[] Members { get; set; }
    }

    /* Corresponds to FPartySyncOneMember */
    public class PartySyncMessageMember
    {
        [JsonProperty]
        public int CharId { get; set; }
        [JsonProperty]
        public int CurHp { get; set; }
        [JsonProperty]
        public int MaxHp { get; set; }

        public PartySyncMessageMember (int charId, int curHp, int maxHp)
        {
            CharId = charId;
            CurHp = curHp;
            MaxHp = maxHp;
        }
    }
}