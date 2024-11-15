using Microsoft.AspNetCore.Hosting.Server;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace PersistenceServer
{
    public class GameLogic
    {
        // When the player first logs in, he gets a connection cookie, and we save him as <cookie, accountId>
        // He then selects a character to log in with, and then loads a new level, which causes a disconnect
        // Once he reconnects, he will send his desired character id and a cookie to the game server
        // The game server will pass it along to the persistence server
        // And we'll check if character id exists on the accountId - if it does, we'll send the character to the game server
        // And the game server will spawn the character for the player
        // And when the player reconnects to the persistence server, he will send exactly the same thing - cookie and character id
        // And we'll log him in, if everything checks out
        // Hence, _connectionCookies survives disconnects
        readonly Dictionary<string, int> _connectionCookies; // <cookie, account id>
        readonly Dictionary<UserConnection, GameServer> _gameServers;
        readonly Dictionary<string, List<GameServer>> _gameServersByZone; // all instances hosting a particular zone
        readonly Dictionary<string, GameServer> _gameServersByGuid;
        readonly Dictionary<string, Party> _partiesByGuid;                
        Dictionary<int, Guild> _guildsById; // all guilds, even if players of said guilds didn't come online this session
        readonly Dictionary<int, string> _playersLastServerId; // when a server sends a GetCharacter RPC, we record this server as last known "owner" of a player
        Dictionary<int, string> _storedPlayerPartyId; // when a player disconnects, we store his previously known party id so he can be reconnected with the party when he returns

        // Dictionaries below do not survive reconnects, they have to be repopulated on reconnects
        readonly Dictionary<UserConnection, int> _accountIdByConnection;
        readonly Dictionary<int, UserConnection> _connectionByAccountId;
        readonly Dictionary<UserConnection, int> _charIdByConnection;
        readonly Dictionary<int, UserConnection> _connectionByCharId;
        readonly Dictionary<string, Player> _playersByName;
        readonly Dictionary<int, Player> _playersById;
        readonly Dictionary<UserConnection, Player> _playersByConnection;

        public GameLogic()
        {
            _connectionCookies = new();
            _accountIdByConnection = new();
            _connectionByAccountId = new();
            _charIdByConnection = new();
            _connectionByCharId = new();
            _playersByName = new();
            _playersById = new();
            _playersByConnection = new();
            _gameServers = new();
            _gameServersByZone = new();
            _gameServersByGuid = new();
            _guildsById =  new();
            _partiesByGuid = new();
            _playersLastServerId = new();
            _storedPlayerPartyId = new();
        }

        public int GetAccountId(UserConnection conn)
        {
            if(_accountIdByConnection.TryGetValue(conn, out var id))
                return id;
            return -1;
        }

        // Called when the user logs in through the initial menu - he's not in the game world and has no character yet
        public void UserLoggedIn(int accountId, string cookie, UserConnection conn)
        {
            conn.Cookie = cookie;
            _connectionCookies.Add(cookie, accountId);
            if (_accountIdByConnection.Remove(conn)) // remove if key exists
            {
                Console.WriteLine("User tried to log in twice, we must never reach here");
                // try to recover
            }
            _accountIdByConnection.Add(conn, accountId);
            // if release, don't allow multiple connections from one account
            // but in debug, it's not unexpected, because we may open two windows in PIE and get two characters from the same account
            // @TODO: maybe we should make a bool allowMultipleCharacters and change ConnectionByAccountId to Dictionary<int, List<UserConnection>>
#if RELEASE
            if (_connectionByAccountId.TryGetValue(accountId, out var oldConn))
            {
                InvalidateCookieForConnection(oldConn);
                DisconnectPlayerFromAllGameServers(oldConn);
                _ = oldConn.Disconnect(); // not awaited
                UserDisconnected(oldConn); // fire this immediately, because otherwise it would fire too late due to threads jumping
            }
#else
            if (!_connectionByAccountId.ContainsKey(accountId))
                _connectionByAccountId.Add(accountId, conn);
#endif
        }
        
        public int GetAccountIdByCookie(string cookie)
        {
            return _connectionCookies.TryGetValue(cookie, out var connectionValue) ? connectionValue : -1;
        }

        // IMPORTANT: don't call it for logged-in characters, because two logged in characters from the same account in PIE will get the same connection,
        // which will likely cause bugs. Unless you explicitely forbid the functionality associated with it in PIE.
        public UserConnection? GetConnectionByAccountId(int accountId)
        {
            return _connectionByAccountId.TryGetValue(accountId, out var connection) ? connection : null;
        }

        public void UserDisconnected(UserConnection conn)
        {
            if (_gameServers.TryGetValue(conn, out GameServer? gameServer))
            {                
                if (_gameServersByZone.TryGetValue(gameServer.Zone, out List<GameServer>? zoneInstances))
                {
                    zoneInstances.Remove(gameServer);
                }
                _gameServersByGuid.Remove(conn.Id.ToString());
                _gameServers.Remove(conn);
                Console.WriteLine($"{DateTime.Now:HH:mm} Game server disconnected");
            }
            if (_charIdByConnection.TryGetValue(conn, out var playerId))
            {
                _connectionByCharId.Remove(playerId);
                _charIdByConnection.Remove(conn);
            }
            if (_accountIdByConnection.TryGetValue(conn, out var userid))
            {
                _connectionByAccountId.Remove(userid);
                _accountIdByConnection.Remove(conn);
            }
            if (_playersByConnection.TryGetValue(conn, out var player))
            {
                var guildId = player.GuildId;
                if (guildId != -1 && _guildsById.TryGetValue(guildId, out var guild)) {
                    guild.OnPlayerDisconnected(player);
                }

                var charname = player.Name;
                var charId = player.CharId;
                Console.WriteLine($"{DateTime.Now:HH:mm} Player disconnected: {charname}");
                _playersByConnection.Remove(conn);
                _playersByName.Remove(charname);
                _playersById.Remove(charId);
            }            
        }

        // Called when the user connects from the game world - he now has a character
        public Player UserReconnected(UserConnection newConn, DatabaseCharacterInfo charInfo)
        {            
            if (_connectionByAccountId.TryGetValue(charInfo.AccountId, out var oldConn))
            {
                // If RELEASE, we don't allow multiple characters from one account
                // But in debug, it's not unexpected, because we may open two windows in PIE and get two characters from the same account
                // @TODO: maybe we should make a bool allowMultipleCharacters and change ConnectionByAccountId to Dictionary<int, List<UserConnection>>
#if RELEASE
                InvalidateCookieForConnection(oldConn);
                DisconnectPlayerFromAllGameServers(oldConn);
                _ = oldConn.Disconnect(); // not awaited
                UserDisconnected(oldConn); // fire this immediately, because otherwise it would fire too late due to threads jumping

                _accountIdByConnection.Add(newConn, charInfo.AccountId);
                _connectionByAccountId.Add(charInfo.AccountId, newConn);
#endif
            }
            else {
                _accountIdByConnection.Add(newConn, charInfo.AccountId);
                _connectionByAccountId.Add(charInfo.AccountId, newConn);
            }

            _connectionByCharId.Add(charInfo.CharId, newConn);
            _charIdByConnection.Add(newConn, charInfo.CharId);
            Player newPlayer = new(newConn, charInfo);
            _playersByConnection.Add(newConn, newPlayer);
            _playersByName.Add(charInfo.Name, newPlayer);
            _playersById.Add(charInfo.CharId, newPlayer);
            if (charInfo.Guild != null)
            {
                if (_guildsById.TryGetValue((int)charInfo.Guild, out var guild))
                    guild.OnPlayerConnected(newPlayer);
                else
                    Console.WriteLine("Player connected with a guild id that doesn't exist. This should never happen, look into it.");
            }
            return newPlayer;
        }

        public void ServerConnected(UserConnection conn, GameServer server)
        {
            _gameServers.Add(conn, server);
            _gameServersByGuid.Add(conn.Id.ToString(), server);
            if (_gameServersByZone.TryGetValue(server.Zone, out List<GameServer>? serverInstancesForZone))
            {
                serverInstancesForZone.Add(server);
            } else
            {
                _gameServersByZone.Add(server.Zone, new List<GameServer> { server });
            }
        }

        public bool IsServer(UserConnection conn)
        {
            return _gameServers.ContainsKey(conn);
        }

        public async Task<GameServer> GetOrStartServerForZone(string zone)
        {
            //@TODO: launch an instance if there isn't a server running a particular zone, or if we're above player limit in all instances
            // but for now, let's just return the first instance
            if (_gameServersByZone.TryGetValue(zone, out List<GameServer>? serverInstances))
            {
                //@TODO: check if there's an instance with sufficient free player slots, if not launch one, etc
                return serverInstances[0];
            }
            else
            {
                // temporarily awaiting Task.CompletedTask to avoid the compiler warning an in incomplete method
                await Task.CompletedTask;
                throw new NotImplementedException("This method is not implemented yet.");
            }
        }

        public UserConnection[] GetAllPlayerConnections()
        {
            return _playersByConnection.Keys.ToArray();
        }

        public Player? GetPlayerByName(string name)
        {
            return _playersByName.TryGetValue(name, out var player) ? player : null;
        }

        public Player? GetPlayerById(int charId)
        {
            return _playersById.TryGetValue(charId, out var player) ? player : null;
        }

        public Player? GetPlayerByConnection(UserConnection conn)
        {
            return _playersByConnection.TryGetValue(conn, out var player) ? player : null;
        }

        public UserConnection? GetConnectionByCharId(int charId)
        {
            return _connectionByCharId.TryGetValue(charId, out var conn) ? conn : null;
        }

        public int GetPlayersOnline()
        {
            return _playersByConnection.Count;
        }

        public GameServer? GetServerByConnection(UserConnection conn)
        {
            return _gameServers.TryGetValue(conn, out var server) ? server : null;
        }

        public GameServer? GetServerByGuid(string guid)
        {
            return _gameServersByGuid.TryGetValue(guid, out var server) ? server : null;
        }

        public string GetPlayerName(UserConnection conn)
        {
            return _playersByConnection.TryGetValue(conn, out var player) ? player.Name : "";
        }

        public UserConnection[] GetAllServerConnections()
        {
            return _gameServers.Keys.ToArray();
        }

        public void DisconnectPlayerFromAllGameServers(UserConnection conn)
        {            
            if (_charIdByConnection.TryGetValue(conn, out var oldCharId))
            {
                byte[] msgToServers = BaseRpc.MergeByteArrays(BaseRpc.ToBytes(RpcType.RpcForceDisconnectPlayer), BaseRpc.ToBytes(oldCharId));
                foreach (var serverConn in GetAllServerConnections())
                {
                    serverConn.Send(msgToServers);
                }
            }
        }

        public void InvalidateCookieForConnection(UserConnection conn)
        {
            _connectionCookies.Remove(conn.Cookie);
        }

        public Guild? GetPlayerGuild(UserConnection conn)
        {
            if (_playersByConnection.TryGetValue(conn, out var player))
            {
                var guildId = player.GuildId;
                return _guildsById.TryGetValue(guildId, out var guild) ? guild : null;
            }
            return null;
        }

        public Guild? GetGuildById(int guildId)
        {
            return _guildsById.TryGetValue(guildId, out var guild) ? guild : null;
        }

        public void AssignGuilds(Dictionary<int, Guild> guilds)
        {
            _guildsById = guilds;
        }

        public void CreateGuild(Guild createdGuild, Player guildLeader)
        {
            _guildsById.Add(createdGuild.Id, createdGuild);
            createdGuild.PopulateMember(guildLeader.CharId, guildLeader.Name, 0);
            createdGuild.OnPlayerConnected(guildLeader);
            guildLeader.GuildId = createdGuild.Id;
            guildLeader.GuildRank = 0; // 0 is leader
        }

        public void DeleteGuild(int guildId)
        {
            _guildsById.Remove(guildId);
        }

        public void SetPlayersServer(int charId, string serverId)
        {
            if (_playersLastServerId.ContainsKey(charId))
            {
                _playersLastServerId[charId] = serverId;
            } else
            {
                _playersLastServerId.Add(charId, serverId);
            }
        }

        public GameServer? GetPlayerServer(int charId)
        {
            if (_playersLastServerId.TryGetValue(charId, out var serverGuid))
            {
                return GetServerByGuid(serverGuid);
            }
            return null;
        }

        public void AddParty(Party party)
        {
            _partiesByGuid.Add(party.Id, party);
        }

        public void RemoveParty(Party party)
        {
            _partiesByGuid.Remove(party.Id);
        }

        public Party? GetPartyById(string Id)
        {
            return _partiesByGuid.TryGetValue(Id, out var party) ? party : null;
        }

        // store party id for a disconnected character
        // if the character reconnects before getting kicked, we'll be able to reassign him his party ref
        public void StoreDisconnectedPlayerPartyId(int charId, string partyId)
        {
            _storedPlayerPartyId[charId] = partyId;
        }

        public void UnstoreDisconnectedPlayerPartyId(int charId)
        {
            _storedPlayerPartyId.Remove(charId);
        }

        public string? GetStoredDisconnectedPlayerPartyId(int charId)
        {
            return _storedPlayerPartyId.TryGetValue(charId, out var partyId) ? partyId : null;
        }

#pragma warning disable CA1822 // remove the "make it static" warning, AddGuildMember may need to operate on fields later on
        public void AddGuildMember(Guild guild, Player player, int rank)
#pragma warning restore CA1822
        {
            guild.PopulateMember(player.CharId, player.Name, rank);
            guild.OnPlayerConnected(player);
            player.GuildId = guild.Id;
            player.GuildRank = rank;
        }

#pragma warning disable CA1822 // remove the "make it static" warning, DeleteGuildMember may need to operate on fields later on
        // deletion assumes the player isn't online, so we only need to remove from Members
        public void DeleteGuildMember(Guild guild, int charId)
#pragma warning restore CA1822
        {
            guild.RemoveMemberById(charId);
        }

#pragma warning disable CA1822 // remove the "make it static" warning, RemoveGuildMember may need to operate on fields later on
        public void RemoveGuildMember(Guild guild, int charId, Player? player)
#pragma warning restore CA1822
        {
            if (player != null)
            {
                guild.OnPlayerDisconnected(player);
                player.GuildId = -1;
                player.GuildRank = -1;
            }
            guild.RemoveMemberById(charId);
        }
    }
}
