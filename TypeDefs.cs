namespace PersistenceServer
{

    public class TypeDefs
    {
        /*
         *
         *    web routes
         *
         */
        public enum Permissions
        {
            FetchAccounts = 0,
            FetchCharacters = 0,
            FetchWorldItems = 0,
            FetchGuilds = 0,
            UpdateGuilds = 2,
            
        }

        public class LoginRequest
        {
            public string? Username { get; set; }
            public string? Password { get; set; }
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }
            public int? Permission { get; set; }
        }

        public class AuthRequest
        {
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }
        }

        public class RequestCharacters
        {
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }
            public int? FetchAccountId { get; set; }
        }

        public class UpdateInventory
        {
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }

            public int selectedCharacterId { get; set; }

            public Inventory? inventory { get; set; }
        }
        
        public class UpdateStats
        {
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }

            public int selectedCharacterId { get; set; }

            public Stats? stats { get; set; }
        }
        
        public class UpdateAppearance
        {
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }

            public int selectedCharacterId { get; set; }

            public Appearance? appearance { get; set; }
        }
        public class UpdateTransform
        {
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }

            public int selectedCharacterId { get; set; }

            public Transform? transform { get; set; }
        }
        
        public class UpdateGuild
        {
            public string? Cookie { get; set; }
            public int? Charid { get; set; }
            public int? Accountid { get; set; }

            public int selectedCharacterId { get; set; }

            public int? guild { get; set; }
            public int? guildRank { get; set; }
        }


        /*
         *
         *  database
         *
         */
        public class WorldItemsDto
        {

            public string name { get; set; }

        }

        public class GuildsDto
        {
            public int id { get; set; }
            public string name { get; set; }
            public string serialized { get; set; }

        }

        public class AccountDto
        {
            public int id { get; set; }
            public string name { get; set; }
            public string steamId { get; set; }
            public string email { get; set; }
            public int status { get; set; }
        }

        public class UserCharactersDto
        {
            public int id { get; set; }
            public int owner { get; set; }
            public string name { get; set; }
            public int guild { get; set; }
            public string guildname { get; set; }
            public int guildrank { get; set; }

            public CharacterSerialized serialized { get; set; }

        }

        public class CharacterSerialized
        {
            public Stats Stats { get; set; }
            public List<string> Buffs { get; set; }
            public Appearance Appearance { get; set; }
            public Transform Transform { get; set; }
            public Inventory Inventory { get; set; }
            public Equipment Equipment { get; set; }
            public string Zone { get; set; }
        }

        public class Stats
        {
            public string charName { get; set; }
            public int curHP { get; set; }
            public int maxHP { get; set; }
            public string rpgClass { get; set; }
        }

        public class Appearance
        {
            public string sex { get; set; }
            public string race { get; set; }
        }

        public class Transform
        {
            public Rotation rotation { get; set; }
            public Translation translation { get; set; }
            public Scale3D scale3D { get; set; }
        }

        public class Rotation
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
            public float W { get; set; }
        }

        public class Translation
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class Scale3D
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }

        public class Inventory
        {
            public int maxSlots { get; set; }
            public List<int> inventoryIndices { get; set; }
            public List<string> inventoryItems { get; set; }
            public List<int> inventoryAmounts { get; set; }
        }

        public class Equipment
        {
            public int maxSlots { get; set; }
            public List<string> equipmentItems { get; set; }
            public List<int> Amounts { get; set; }
        }

    }
}