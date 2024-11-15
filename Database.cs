using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

namespace PersistenceServer
{
    public abstract class Database
    {
        protected string ConnectionParams;
        // Change Pepper to any random string before creating any user accounts. Once users are created, don't ever change it again.
        protected readonly string Pepper = "$2a$11$46Z/ZIevW5fGpZFXJK5CMe";
        protected string GetIdentitySqlCommand;

#pragma warning disable CS8618 // ignore CS8618 because it's an abstract class
        protected Database(SettingsReader settings) { }
#pragma warning restore CS8618

        // Checks that there is a database, if not creates one
        public abstract Task CheckCreateDatabase(SettingsReader settings);
        // Always call with 'using' keyword or close manually
        protected abstract Task<DbConnection> GetConnection(string parameters);
        protected abstract DbCommand GetCommand(string parameters, DbConnection? connection);
        protected virtual DbCommand GetCommand(string parameters) => GetCommand(parameters, null);

        protected async Task<DataTable> RunQuery(string cmdParams, string overrideConnectionParams)
        {
            await using var conn = await GetConnection(overrideConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            DataTable dt = new();
            dt.Load(reader);
            return dt;
        }

        protected async Task<DataTable> RunQuery(string cmdParams)
        {
            await using var conn = await GetConnection(ConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            DataTable dt = new();
            dt.Load(reader);
            return dt;
        }

        protected async Task<DataTable> RunQuery(DbCommand command)
        {
            await using var conn = await GetConnection(ConnectionParams);
            command.Connection = conn;
            await using var reader = await command.ExecuteReaderAsync();
            DataTable dt = new();
            dt.Load(reader);
            await command.DisposeAsync();
            return dt;
        }

        // Returns the number of rows affected
        protected async Task<int> RunNonQuery(string cmdParams, string overrideConnectionParams)
        {
            await using var conn = await GetConnection(overrideConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            return await cmd.ExecuteNonQueryAsync();
        }

        // Returns the number of rows affected
        protected async Task<int> RunNonQuery(string cmdParams)
        {
            await using var conn = await GetConnection(ConnectionParams);
            await using var cmd = GetCommand(cmdParams, conn);
            return await cmd.ExecuteNonQueryAsync();
        }

        // Returns the number of rows affected
        protected async Task<int> RunNonQuery(DbCommand command)
        {
            await using var conn = await GetConnection(ConnectionParams);
            command.Connection = conn;
            int rowsAffected = await command.ExecuteNonQueryAsync();
            await command.DisposeAsync();
            return rowsAffected;
        }

        //protected async Task<int> RunScalar(string cmdParams, string overrideConnectionParams)
        //{
        //    await using var conn = await GetConnection(overrideConnectionParams);
        //    await using var cmd = GetCommand(cmdParams, conn);
        //    await cmd.ExecuteNonQuery();
        //}

        //protected async Task<int> RunScalar(string cmdParams)
        //{
        //    await using var conn = await GetConnection(ConnectionParams);
        //    await using var cmd = GetCommand(cmdParams, conn);
        //    await cmd.ExecuteNonQuery();
        //}

        // Returns the last inserted row id
        private async Task<int> RunInsert(DbCommand command)
        {
            command.CommandText += GetIdentitySqlCommand;
            await using var conn = await GetConnection(ConnectionParams);
            command.Connection = conn;
            object? obj = await command.ExecuteScalarAsync();
            await command.DisposeAsync();
            return (int)Convert.ChangeType(obj!, typeof(int));
        }

        public async Task HelloWorld()
        {
            //var dt = await RunQuery(@"SET @helloWorldStr = ""Hello World!"";SELECT @helloWorldStr AS ""My Hello World"";");
            var cmd = GetCommand(@"SET @helloWorldStr = ""Hello World!"";SELECT @helloWorldStr AS ""My Row"";");
            var dt = await RunQuery(cmd);
            Debug.Assert(dt.HasRows());            
            Console.WriteLine(dt.Rows[0]["My Row"].ToString());            
        }

        /******************** Below are gameplay related DB requests ****************/

        public virtual async Task<bool> DoesAccountExist(string accountName)
        {
            var cmd = GetCommand("SELECT * FROM accounts WHERE name = @accountName");
            cmd.AddParam("@accountName", accountName);
            var dt = await RunQuery(cmd);
            return dt.HasRows();
        }

        public virtual async Task<int> CreateUserAccount(string accountName, string password)
        {
            string salt = BCrypt.Net.BCrypt.GenerateSalt();
            var cmd = GetCommand("INSERT INTO `accounts` (`id`, `name`, `steamid`, `password`, `salt`, `email`, `status`) VALUES (NULL, @accountName, NULL, @password, @salt, NULL, 0);");
            cmd.AddParam("@accountName", accountName);
            cmd.AddParam("@password", BCrypt.Net.BCrypt.HashPassword(password, salt + Pepper));
            cmd.AddParam("@salt", salt);
            int lastInsertedId = await RunInsert(cmd);
            return lastInsertedId;
        }

        public virtual async Task<int> CreateSteamAccount(string steamId)
        {
            var cmd = GetCommand("INSERT INTO `accounts` (`id`, `name`, `steamid`, `password`, `salt`, `email`, `status`) VALUES (NULL, NULL, @steamid, NULL, NULL, NULL, 0);");
            cmd.AddParam("@steamid", steamId);
            int lastInsertedId = await RunInsert(cmd);
            return lastInsertedId;
        }

        /* Returns: returns -1 if login failes, otherwise returns the user account's id
         * It's different for Sqlite and MySQL because of how passwords are stored, 
         * though I'm not sure why it's recommended to store BCrypt hashes as binary arrays (sqlite doesn't even have them)
         * If you know something about it, let me know. */
        public abstract Task<int> LoginUser(string accountName, string password);

        public virtual async Task<int> LoginSteamUser(string steamId)
        {
            var cmd = GetCommand("SELECT id, status FROM accounts WHERE steamid = @steamId");
            cmd.AddParam("@steamId", steamId);
            var dt = await RunQuery(cmd);

            // if no account with this steamid is found, we should create it!
            if (!dt.HasRows())
            {
                int newId = await CreateSteamAccount(steamId);
                return newId;
            }
            // if it's found
            else
            {
                var id = (int)dt.GetInt(0, "id")!;
                var status = dt.GetInt(0, "status")!;

                // if status is banned
                if (status == -1)
                {
                    return -1;
                }

                // if everything checks out, allow login by returning user's id
                return id;
            }
        }

        public virtual async Task<bool> DoesCharnameExist(string charName)
        {
            var cmd = GetCommand("SELECT * FROM characters WHERE name = @charName");
            cmd.AddParam("@charName", charName);
            var dt = await RunQuery(cmd);
            return dt.HasRows();
        }

        public virtual async Task<int> CreateCharacter(string charName, int ownerAccountId, string serializedCharacter)
        {
            var cmd = GetCommand("INSERT INTO `characters` (`id`, `name`, `owner`, `guild`, `guildrank`, `serialized`) VALUES (NULL, @charName, @ownerAccountId, NULL, NULL, @serialized);");
            cmd.AddParam("@charName", charName);
            cmd.AddParam("@ownerAccountId", ownerAccountId);
            cmd.AddParam("@serialized", serializedCharacter);
            int lastInsertedId = await RunInsert(cmd);
            return lastInsertedId;
        }

        public virtual async Task<List<Tuple<int, string, string>>> GetCharacters(int accountId)
        {
            List<Tuple<int, string, string>> result = new();

            var cmd = GetCommand("SELECT * FROM characters WHERE owner = @accountId");
            cmd.AddParam("@accountId", accountId);
            var dt = await RunQuery(cmd);
            foreach (var row in dt.Rows.OfType<DataRow>())
            {
                var id = row.GetInt("id");
                var name = row.GetString("name");
                var serialized = row.GetString("serialized");
                result.Add(new Tuple<int, string, string>((int)id!, name!, serialized!));
            }

            return result;
        }

        /* Returns: name, serialized, guild, guildrank */
        public virtual async Task<Tuple<string, string, int?, int?>?> GetCharacter(int charId, int accountId)
        {
            var cmd = GetCommand("SELECT * FROM characters WHERE id = @charId and owner = @accountId");
            cmd.AddParam("@charId", charId);
            cmd.AddParam("@accountId", accountId);
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return null;
            var row = dt.Rows[0];
            string charname = row.GetString("name")!;
            string serialized = row.GetString("serialized")!;
            int? guild = row.GetInt("guild");
            int? guildrank = row.GetInt("guildrank");
            return new Tuple<string, string, int?, int?>(charname, serialized, guild, guildrank);
        }

        // Returns true/false and optionally a charname
        public virtual async Task<string?> DoesAccountOwnCharacter(int accountId, int charId)
        {
            var cmd = GetCommand("SELECT * FROM characters WHERE id = @charId and owner = @accountId");
            cmd.AddParam("@charId", charId);
            cmd.AddParam("@accountId", accountId);
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return null;
            var row = dt.Rows[0];
            string charname = row.GetString("name")!;
            return charname;
        }

        public virtual async Task<Tuple<int, string, string, int, int?, int?>?> GetCharacterForPieWindow(int pieWindowId)
        {
            var cmd = GetCommand("SELECT * FROM `characters` ORDER BY id ASC LIMIT @pieWindowId,1 ");
            cmd.AddParam("@pieWindowId", pieWindowId);
            var dt = await RunQuery(cmd);
            if (!dt.HasRows()) return null;
            var row = dt.Rows[0];
            int charId = (int)row.GetInt("id")!;
            string charname = row.GetString("name")!;
            string serialized = row.GetString("serialized")!;
            int owner = (int)row.GetInt("owner")!;
            int? guild = row.GetInt("guild");
            int? guildrank = row.GetInt("guildrank");
            return new Tuple<int, string, string, int, int?, int?>(charId, charname, serialized, owner, guild, guildrank);
        }

        public async Task SaveCharacter(int charId, string serializedCharacter)
        {
            var cmd = GetCommand("UPDATE `characters` SET serialized = @serializedChar WHERE id = @charId");
            cmd.AddParam("@charId", charId);
            cmd.AddParam("@serializedChar", serializedCharacter);
            int result = await RunNonQuery(cmd);
            if (result == 1) Console.WriteLine($"Character with id {charId} was saved to DB.");
            else Console.WriteLine($"Character wasn't saved: {charId}!");
        }
    }
}
