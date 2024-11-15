using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace PersistenceServer
{
    public class DatabaseSqlite : Database
    {
        public DatabaseSqlite(SettingsReader settings) : base(settings)
        {
            ConnectionParams = $"Data Source={settings.SqliteFilename};";
            GetIdentitySqlCommand = "SELECT last_insert_rowid();";
        }

        protected override async Task<DbConnection> GetConnection(string parameters)
        {
            var connection = new SqliteConnection(parameters);
            await connection.OpenAsync();
            return connection;
        }

        protected override DbCommand GetCommand(string parameters, DbConnection? connection) => new SqliteCommand(parameters, (SqliteConnection?)connection);

        public override async Task CheckCreateDatabase(SettingsReader settings)
        {
            // get the number of tables in the database (from a special table called sqlite_master)
            var getTablesQuery = await RunQuery($"SELECT count(*) FROM sqlite_master WHERE type = 'table';");
            // if there's 0 tables, it means the database is new, so we create them
            if (getTablesQuery.GetBigInt(0, "count(*)") == 0) // count returns BigInt
            {
                Console.Write("Database not found or is empty: creating... ");

                // create tables
                await RunNonQuery(
                    @"CREATE TABLE ""accounts"" (
	                    ""id""	INTEGER NOT NULL UNIQUE,
	                    ""name""	TEXT UNIQUE,
                        ""steamid""	TEXT UNIQUE,
	                    ""password""	TEXT,
	                    ""salt""	TEXT,
	                    ""email""	TEXT,
	                    ""status""	INTEGER,
	                    PRIMARY KEY(""id"" AUTOINCREMENT),
	                    UNIQUE(""name"")
                    );
                    CREATE UNIQUE INDEX accname 
                    ON accounts(name);
                    CREATE UNIQUE INDEX accsteamid 
                    ON accounts(steamid);
                    CREATE TABLE ""guilds"" (
	                    ""id""	INTEGER NOT NULL UNIQUE,
	                    ""name""	TEXT UNIQUE,
	                    PRIMARY KEY(""id"" AUTOINCREMENT),
	                    UNIQUE(""name"")
                    );
                    CREATE UNIQUE INDEX guildname 
                    ON guilds(name);
                    CREATE TABLE ""characters"" (
	                    ""id""	INTEGER NOT NULL UNIQUE,
	                    ""name""	TEXT UNIQUE,
                        ""owner""	INTEGER,
	                    ""guild""	INTEGER,
	                    ""guildrank""	INTEGER,
	                    ""serialized""	TEXT,	
	                    PRIMARY KEY(""id"" AUTOINCREMENT),
	                    UNIQUE(""name""),
                        FOREIGN KEY(""owner"") REFERENCES ""accounts""(""id"") ON UPDATE CASCADE ON DELETE SET NULL,
                        FOREIGN KEY(""guild"") REFERENCES ""guilds""(""id"") ON UPDATE CASCADE ON DELETE SET NULL
                    );
                    CREATE UNIQUE INDEX charname
                    ON characters(name);
                    CREATE INDEX charowner
                    ON characters(owner);
                    CREATE INDEX charguild
                    ON characters(guild);
                    ");
                // ~create tables

                Console.WriteLine("done.");
            }
            else
            {
                Console.WriteLine($"Database found: {settings.SqliteFilename}");
            }
        }

        public override async Task<int> LoginUser(string accountName, string password)
        {
            var cmd = GetCommand("SELECT id, password, salt, status FROM accounts WHERE name = @accountName");
            cmd.AddParam("@accountName", accountName);
            var dt = await RunQuery(cmd);

            // if not account with this name is found
            if (!dt.HasRows())
            {
                return -1;
            }

            var id = (int)dt.GetBigInt(0, "id")!;
            var status = dt.GetBigInt(0, "status");
            var passwordInDb = dt.GetString(0, "password");
            var salt = dt.GetString(0, "salt");

            // if status is banned
            if (status == -1)
            {
                return -1;
            }

            // if wrong password
            if (passwordInDb != BCrypt.Net.BCrypt.HashPassword(password, salt + Pepper))
            {
                return -1;
            }

            // if everything checks out, allow login by returning user's id
            return id;
        }
    }
}
