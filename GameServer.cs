using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer
{
    public class GameServer
    {
        public int Port;
        public string Level;
        public string Zone; // can be an empty string if the level doesn't have zones
        public UserConnection Conn; // has an Ip field
        public int ConnectedPlayers = 0;
        public HashSet<int> CharIds; // characters that are online on this server

        public GameServer(int port, string level, string zone, UserConnection conn)
        {
            Port = port;
            Level = level;
            Conn = conn;
            Zone = zone;
            CharIds = new();
        }

        public void AddCharacterToServer(int charId)
        {
            CharIds.Add(charId);
        }

        public void RemoveCharacterFromServer(int charId)
        {
            CharIds.Remove(charId);
        }

        public Guid GetGuid()
        {
            return Conn.Id;
        }
    }
}
