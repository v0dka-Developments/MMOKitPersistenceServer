using Newtonsoft.Json;
using Microsoft.AspNetCore.Hosting.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using System.Xml.Linq;

namespace PersistenceServer
{
    public class Party
    {        
        public string Id { get; set; }
        public int PartyLeaderId { get; set; }
        public List<int> MemberIds { get; set; }

        // To use when player is not online, but party info needs to be re-sent
        private readonly Dictionary<int, PartyMemberInfo> CachedMembersInfo;

        public static readonly int MaxMembers = 5;

        public Party(Player Leader, Player Member)
        {
            Id = Guid.NewGuid().ToString();
            PartyLeaderId = Leader.CharId;
            MemberIds = [];
            CachedMembersInfo = [];

            MmoWsServer.Singleton!.GameLogic.AddParty(this);

            AddMemberNoReplication(Leader);
            AddMemberNoReplication(Member);

            SendMemberJoined(Member);
            SendFullPartyToInvolvedServers();
        }

        public bool IsPartyFull()
        {
            return MemberIds.Count >= MmoWsServer.Singleton!.Settings.PartyMaxSize;
        }

        private void AddMemberNoReplication(Player player)
        {
            player.PartyRef = this;
            MemberIds.Add(player.CharId);
            CachedMembersInfo.Add(player.CharId, new PartyMemberInfo(player.Name));
        }

        public void AddMember(Player player)
        {
            player.PartyRef = this;
            MemberIds.Add(player.CharId);
            CachedMembersInfo.Add(player.CharId, new PartyMemberInfo(player.Name));
            SendMemberJoined(player);
            SendFullPartyToInvolvedServers();
        }

        // When removing by id, the character may be offline, so we may not have a valid Player reference
        public void RemoveMemberById(int id, bool voluntarily)
        {
            if (MemberIds.Count > 2)
            {
                if (MmoWsServer.Singleton!.GameLogic.GetConnectionByCharId(id) is UserConnection conn)
                {
                    if (MmoWsServer.Singleton!.GameLogic.GetPlayerByConnection(conn) is Player player)
                    {
                        player.PartyRef = null;
                    }
                }
                SendMemberLeft(GetMemberName(id), voluntarily);
                MmoWsServer.Singleton!.GameLogic.UnstoreDisconnectedPlayerPartyId(id);                
                MemberIds.Remove(id);                
                SendFullPartyToInvolvedServers();
                CachedMembersInfo.Remove(id);
            }
            else
            {
                SendMemberLeft(GetMemberName(id), voluntarily);
                DisbandParty(false);
            }
        }

        public void RemoveMember(Player player, bool voluntarily)
        {
            if (MemberIds.Count > 2)
            {
                player.PartyRef = null;
                SendMemberLeft(GetMemberName(player.CharId), voluntarily);
                MmoWsServer.Singleton!.GameLogic.UnstoreDisconnectedPlayerPartyId(player.CharId);
                MemberIds.Remove(player.CharId);
                SendFullPartyToInvolvedServers();
                CachedMembersInfo.Remove(player.CharId);
            } 
            else
            {
                SendMemberLeft(GetMemberName(player.CharId), voluntarily);
                DisbandParty(false);
            }
        }

        public void DisbandParty(bool sendDisbandMessage)
        {
            Console.WriteLine($"Party disbanded: {Id}");

            // send message to party members first, while they're still valid
            // and then remove their PartyRef
            byte[] msgToClients = BaseRpc.MergeByteArrays(BaseRpc.ToBytes(RpcType.RpcPartyDisband));            
            foreach (var memberCharId in MemberIds)
            {
                MmoWsServer.Singleton!.GameLogic.UnstoreDisconnectedPlayerPartyId(memberCharId);

                if (MmoWsServer.Singleton!.GameLogic.GetConnectionByCharId(memberCharId) is UserConnection clientConn)
                {
                    if (sendDisbandMessage)
                    {
                        clientConn.Send(msgToClients);
                    }

                    var player = MmoWsServer.Singleton!.GameLogic.GetPlayerByConnection(clientConn);
                    if (player != null)
                    {
                        player.PartyRef = null;
                    }
                }
            }            

            // get server connections first, before clearing memberids
            var serverConnections = GetInvolvedServerConnections();
            MemberIds.Clear();
            PartyLeaderId = -1;
            string partyJsonString = JsonConvert.SerializeObject(new PartyJson(this));
            byte[] msg = BaseRpc.MergeByteArrays(BaseRpc.ToBytes(RpcType.RpcPartyFullInfo), BaseRpc.WriteMmoString(partyJsonString));
            foreach (UserConnection conn in serverConnections)
            {
                conn.Send(msg);
            }

            MmoWsServer.Singleton!.GameLogic.RemoveParty(this);

        }

        public string GetMemberName(int charId)
        {
            if (MmoWsServer.Singleton!.GameLogic.GetConnectionByCharId(charId) is UserConnection conn)
            {
                if (MmoWsServer.Singleton!.GameLogic.GetPlayerByConnection(conn) is Player player)
                {
                    return player.Name;
                }
            }
            return CachedMembersInfo[charId].Name;
        }

        public void UpdateMemberStats(int charId, int newCurHp, int newMaxHp)
        {
            if (CachedMembersInfo.TryGetValue(charId, out PartyMemberInfo? stats))
            {
                stats.CurHp = newCurHp;
                stats.MaxHp = newMaxHp;
            }
        }

        public PartyMemberInfo? GetMemberStats(int charId)
        {
            return CachedMembersInfo.TryGetValue(charId, out var stats) ? stats : null;
        }

        public bool GetMemberOnline(int charId)
        {
            return MmoWsServer.Singleton!.GameLogic.GetPlayerByName(CachedMembersInfo[charId].Name) != null;
        }

        public bool HasOnlineMembers()
        {
            foreach (int memberId in MemberIds)
            {
                if (GetMemberOnline(memberId)) 
                    return true;
            }
            return false;
        }

        /* Sends a player joined your party message to clients */
        public void SendMemberJoined(Player joiner)
        {
            byte[] msg = BaseRpc.MergeByteArrays(BaseRpc.ToBytes(RpcType.RpcPartyJoin), BaseRpc.WriteMmoString(joiner.Name));
            foreach (UserConnection conn in GetClientConnections())
            {
                conn.Send(msg);
            }
        }

        /* Sends a player left your party message to clients */
        public void SendMemberLeft(string charName, bool voluntarily)
        {
            byte[] msg = BaseRpc.MergeByteArrays(BaseRpc.ToBytes(RpcType.RpcPartyLeave), BaseRpc.WriteMmoString(charName), BaseRpc.ToBytes(voluntarily));
            foreach (UserConnection conn in GetClientConnections())
            {
                conn.Send(msg);
            }
        }

        /* Whenever a party info gets updated, send it to all game servers with these players online
         * We trust the game servers to make a diff between old party info and new one to determine who left, etc...
         * The game servers will update the info on PlayerController, and it'll be replicated to game clients */
        public void SendFullPartyToInvolvedServers()
        {
            string partyJsonString = JsonConvert.SerializeObject(new PartyJson(this));
            byte[] msg = BaseRpc.MergeByteArrays(BaseRpc.ToBytes(RpcType.RpcPartyFullInfo), BaseRpc.WriteMmoString(partyJsonString));
            foreach (UserConnection conn in GetInvolvedServerConnections())
            {
                conn.Send(msg);
            }
        }

        public List<UserConnection> GetClientConnections()
        {
            List<UserConnection> connections = [];
            foreach (var memberCharId in MemberIds)
            {
                if (MmoWsServer.Singleton!.GameLogic.GetConnectionByCharId(memberCharId) is UserConnection clientConn)
                {
                    connections.Add(clientConn);
                }
            }
            return connections;
        }

        /* Gets all client connections of the party members
        Plus all server connections where party members play */
        public HashSet<UserConnection> GetClientAndServerConnections()
        {
            HashSet<UserConnection> connections = [];
            foreach (var memberCharId in MemberIds)
            {
                if (MmoWsServer.Singleton!.GameLogic.GetConnectionByCharId(memberCharId) is UserConnection clientConn)
                {
                    connections.Add(clientConn);
                    if (MmoWsServer.Singleton!.GameLogic.GetPlayerServer(memberCharId) is GameServer gameServer)
                    {
                        connections.Add(gameServer.Conn);
                    }
                }
            }
            return connections;
        }

        /* Get all server connections where party members are online */
        public HashSet<UserConnection> GetInvolvedServerConnections()
        {
            HashSet<UserConnection> connections = [];
            foreach (var memberCharId in MemberIds)
            {
                if (MmoWsServer.Singleton!.GameLogic.GetPlayerServer(memberCharId) is GameServer gameServer)
                {
                    connections.Add(gameServer.Conn);
                }
            }
            return connections;
        }

    }

    public class PartyJson
    {
        [JsonProperty]
        public string PartyId { get; set; }
        [JsonProperty]
        public int LeaderId { get; set; }
        [JsonProperty]
        public int[] MemberIds { get; set; }
        [JsonProperty]
        public string[] MemberNames { get; set; }
        [JsonProperty]
        public bool[] MembersOnline { get; set; }

        public PartyJson(Party inParty)
        {
            PartyId = inParty.Id;
            LeaderId = inParty.PartyLeaderId;
            MemberIds = inParty.MemberIds.ToArray();
            MemberNames = new string[MemberIds.Length];
            MembersOnline = new bool[MemberIds.Length];
            for (int i = 0; i < MemberIds.Length; i++)
            {
                MemberNames[i] = inParty.GetMemberName(MemberIds[i]);
                MembersOnline[i] = inParty.GetMemberOnline(MemberIds[i]);
            }
        }
    }
}
