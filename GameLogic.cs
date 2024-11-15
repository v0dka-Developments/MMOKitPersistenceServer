namespace PersistenceServer
{
    public struct Player
    {
        public string Name;
        public UserConnection Conn;
        public int CharId;
        public int AccountId;
        public Player(UserConnection conn, int userId, int charId, string charName)
        {
            Name = charName;
            Conn = conn;
            CharId = charId;
            AccountId = userId;
        }
    }

    public class GameLogic
    {
        // When the player first logs in, he gets a connection cookie, and we save him as <cookie, userid>
        // He then selects a character to log in with, and then loads a new level, which causes a disconnect
        // Once he reconnects, he will send his desired character id and a cookie to the game server
        // The game server will pass it along to the persistence server
        // And we'll check if character id exists on the account - if it does, we'll send the character to the game server
        // And the game server will spawn the character for the player
        // And when the player reconnects to the persistence server, he will send exactly the same thing - cookie and character id
        // And we'll log him in, if everything checks out
        // Hence, ConnectionCookies survive disconnects
        Dictionary<string, int> _connectionCookies;
        HashSet<UserConnection> _gameServers;
        // Dictionaries below do not survive reconnects, they have to be repopulated on reconnects
        Dictionary<UserConnection, int> _accountIdByConnection;
        Dictionary<int, UserConnection> _connectionByAccountId;
        Dictionary<UserConnection, int> _charIdByConnection;
        Dictionary<int, UserConnection> _connectionByCharId;
        Dictionary<string, Player> _playersByName;
        Dictionary<UserConnection, Player> _playersByConnection;

        public GameLogic()
        {
            _connectionCookies = new();
            _accountIdByConnection = new();
            _connectionByAccountId = new();
            _charIdByConnection = new();
            _connectionByCharId = new();
            _playersByName = new();
            _playersByConnection = new();
            _gameServers = new();
        }

        public int GetAccountId(UserConnection conn)
        {
            return _accountIdByConnection.ContainsKey(conn) ? _accountIdByConnection[conn] : -1;
        }

        // Called when the user logs in through the initial menu - he's not in the game world and has no character yet
        public void UserLoggedIn(int userId, string cookie, UserConnection conn)
        {
            _connectionCookies.Add(cookie, userId);
            if (_accountIdByConnection.ContainsKey(conn))
            {
                Console.WriteLine("User tried to log in twice, we must never reach here");
                // try to recover
                _accountIdByConnection.Remove(conn);
            }
            _accountIdByConnection.Add(conn, userId);
            // if release, don't allow multiple characters from one account
            // but in debug, it's not unexpected, because we may open two windows in PIE and get two characters from the same account
            // @TODO: maybe we should make a bool allowMultipleCharacters and change ConnectionByAccountId to Dictionary<int, List<UserConnection>>
#if RELEASE
            if (_connectionByAccountId.ContainsKey(userId))
            {
                var oldConn = _connectionByAccountId[userId];
                oldConn.Disconnect();
                UserDisconnected(oldConn); // fire this immediately, because otherwise it would fire too late due to threads jumping
                //@TODO: tell servers to disconnect this userid!
            }
#else
            if (!_connectionByAccountId.ContainsKey(userId))
                _connectionByAccountId.Add(userId, conn);
#endif
        }

        public int GetAccountIdByCookie(string cookie)
        {
            return _connectionCookies.ContainsKey(cookie) ? _connectionCookies[cookie] : -1;
        }

        public void UserDisconnected(UserConnection conn)
        {
            if (_gameServers.Contains(conn))
            {
                Console.WriteLine("Game server disconnected");
                _gameServers.Remove(conn);
            }
            if (_charIdByConnection.ContainsKey(conn))
            {
                var playerId = _charIdByConnection[conn];
                _connectionByCharId.Remove(playerId);
                _charIdByConnection.Remove(conn);
            }
            if (_accountIdByConnection.ContainsKey(conn))
            {
                var userid = _accountIdByConnection[conn];
                _connectionByAccountId.Remove(userid);
                _accountIdByConnection.Remove(conn);
            }
            if (_playersByConnection.ContainsKey(conn))
            {                
                var charname = _playersByConnection[conn].Name;
                Console.WriteLine($"Player disconnected: {charname}");
                _playersByConnection.Remove(conn);
                _playersByName.Remove(charname);
            }
        }

        // Called when the user connects from the game world - he now has a character
        public void UserReconnected(UserConnection newConn, int userId, int charId, string charName)
        {
            if (_connectionByAccountId.ContainsKey(userId))
            {
                // if release, don't allow multiple characters from one account
                // but in debug, it's not unexpected, because we may open two windows in PIE and get two characters from the same account
                // @TODO: maybe we should make a bool allowMultipleCharacters and change ConnectionByAccountId to Dictionary<int, List<UserConnection>>
#if RELEASE
                var oldConn = _connectionByAccountId[userId];
                oldConn.Disconnect();
                UserDisconnected(oldConn); // fire this immediately, because otherwise it would fire too late due to threads jumping
                //@TODO: tell servers to disconnect this userid!

                _accountIdByConnection.Add(newConn, userId);
                _connectionByAccountId.Add(userId, newConn);
#endif
            }
            else {
                _accountIdByConnection.Add(newConn, userId);
                _connectionByAccountId.Add(userId, newConn);
            }
            
            _connectionByCharId.Add(charId, newConn);
            _charIdByConnection.Add(newConn, charId);
            Player newPlayer = new Player(newConn, userId, charId, charName);
            _playersByConnection.Add(newConn, newPlayer);
            _playersByName.Add(charName, newPlayer);
        }

        public void ServerConnected(UserConnection conn)
        {
            _gameServers.Add(conn);
        }

        public bool IsServer(UserConnection conn)
        {
            return _gameServers.Contains(conn);
        }

        public UserConnection[] GetAllPlayerConnections()
        {
            return _playersByConnection.Keys.ToArray();
        }

        public Player? GetPlayerByName(string name)
        {
            if (_playersByName.ContainsKey(name))
                return _playersByName[name];
            return null;
        }

        public Player? GetPlayerByConnection(UserConnection conn)
        {
            if (_playersByConnection.ContainsKey(conn))
                return _playersByConnection[conn];
            return null;
        }

        public string GetPlayerName(UserConnection conn)
        {
            if (_playersByConnection.ContainsKey(conn)) return _playersByConnection[conn].Name;
            else return "";
        }

        public UserConnection[] GetAllServerConnections()
        {
            return _gameServers.ToArray();
        }
    }
}
