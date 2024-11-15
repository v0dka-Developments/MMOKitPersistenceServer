using IniParser.Model;
using IniParser;

namespace PersistenceServer
{
    public enum SqlType
    {
        Undefined,
        Sqlite,
        MySql
    }

    public class SettingsReader
    {
        public readonly SqlType SqlType;
        public readonly int Port;
        public readonly string PersistenceServerIP;
        public readonly string ServerPassword; // password with which game servers can log in
        public readonly string UniversalCookie; // cookie used by clients connecting from PIE, who subsequently don't have a valid cookie, but still need a character
        public readonly string SteamWebApiKey;
        public readonly string SteamAppId;
        public readonly int DefaultGuildRank;
        public readonly int GuildOfficerRank;
        public readonly int PartyMaxSize;

        public readonly string? MysqlHost;
        public readonly int? MysqlPort;
        public readonly string? MysqlUser;
        public readonly string? MysqlPassword;
        public readonly string? MysqlDatabase;
        public readonly bool MysqlAccentSensitiveCollation;

        public readonly string? SqliteFilename;        

        public SettingsReader()
        {
            FileIniDataParser parser = new();
            IniData data = parser.ReadFile("settings.ini");

            Port = int.Parse(data["General"]["port"]);
            DefaultGuildRank = int.Parse(data["General"]["DefaultGuildRank"]);
            GuildOfficerRank = int.Parse(data["General"]["GuildOfficerRank"]);
            PartyMaxSize = int.Parse(data["General"]["PartyMaxSize"]);

            var dms = data["General"]["dms"];
#if DEBUG_MYSQL
            dms = "mysql";
#elif DEBUG_SQLITE
            dms = "sqlite";
#endif
            Console.WriteLine($"DB system: {dms}");

            PersistenceServerIP = data["General"]["PersistenceServerIP"];
            Console.WriteLine("Persistence Server IP: " + PersistenceServerIP);

            ServerPassword = data["General"]["ServerPassword"];
            UniversalCookie = data["General"]["UniversalCookie"];

            SteamWebApiKey = data["Steam"]["SteamPublisherKey"];
            SteamAppId = data["Steam"]["SteamDevAppId"];

            if (dms.ToLower() == "mysql")
            {
                SqlType = SqlType.MySql;
                MysqlHost = data["MySQL"]["host"];
                MysqlPort = int.Parse(data["MySQL"]["port"]);
                MysqlUser = data["MySQL"]["user"];
                MysqlPassword = data["MySQL"]["password"];
                MysqlDatabase = data["MySQL"]["database"];
                MysqlAccentSensitiveCollation = bool.Parse(data["MySQL"]["accentSensitiveCollation"]);
            }

            if (dms.ToLower() == "sqlite")
            {
                SqlType = SqlType.Sqlite;
                SqliteFilename = data["Sqlite"]["filename"];
            }
        }
    }
}
