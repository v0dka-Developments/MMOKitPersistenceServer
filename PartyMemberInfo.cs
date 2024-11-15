using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersistenceServer
{
    // this is info that the GameServer sends to the Persistence Server once every couple of seconds
    // we send this info back to other party members when they request it (which they also do every couple of seconds)
    // see Party in the documentation
    public class PartyMemberInfo
    {
        public string Name;
        public int CurHp;
        public int MaxHp;

        public PartyMemberInfo(string inName)
        {
            Name = inName;
            CurHp = 1;
            MaxHp = 1;
        }
    }
}
