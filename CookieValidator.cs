/*
 *
 *  needed a way to invalidate cookies when invalid, no access, access removal etc..
 *  so cookievalidator is a quick implementation, if got time will switch to jwt auth
 *  currently it works by generating a bcrypt string splitting it in half, sending half to the client and keeping the other half on the server
 *  client sends their half with every request and the client half + server half put together is a valid hash then we are cooking with gas! they have
 *  access woo!
 *
 *  we can also refresh cookies and invalidate cookies
 * 
 */

namespace PersistenceServer
{
    public class CookieValidator
    {
        private readonly Dictionary<int, string> _userCookies;

        // Constructor to initialize the dictionary of user cookies
        public CookieValidator()
        {
            _userCookies = new Dictionary<int, string>();
        }

        // Method to store the bcrypt hash part for the user
        public void StoreUserCookie(int accountId, string cookiePart)
        {
            _userCookies[accountId] = cookiePart;
        }

        // Method to validate the cookie sent by the client
        public bool ValidateCookie(int accountId, string clientCookie)
        {
            if (_userCookies.ContainsKey(accountId))
            {
                string storedCookie = _userCookies[accountId];
                string finalCookie = clientCookie + storedCookie;
                
                // Verify that the cookie matches the expected hash
                return BCrypt.Net.BCrypt.Verify(accountId.ToString(), finalCookie);
            }
            Console.WriteLine("Account ID not found");
            return false; // User doesn't exist or invalid cookie
        }

        // Method to generate a new token for a given accountId
        public (string cookiePart, string serverPart) GenerateToken(int accountId)
        {
            var bcryptHash = BCrypt.Net.BCrypt.HashPassword(accountId.ToString());
            
            // Split the hash into two parts: client and server
            var cookiePart = bcryptHash.Substring(0, bcryptHash.Length / 2); // First part for client
            var serverPart = bcryptHash.Substring(bcryptHash.Length / 2); // Second part to store on server
            
            // Store the server part for later validation
            StoreUserCookie(accountId, serverPart);
            
            return (cookiePart, serverPart);
        }

        // Method to invalidate a token (delete the token from the dictionary)
        public bool InvalidateToken(int accountId)
        {
            if (_userCookies.ContainsKey(accountId))
            {
                _userCookies.Remove(accountId);
                Console.WriteLine("Token invalidated for account ID: " + accountId);
                return true;  // Return true to indicate that the token was successfully invalidated
            }
            else
            {
                Console.WriteLine("Token not found for account ID: " + accountId);
                return false;  // Return false if no token was found for the account
            }
        }

        // Method to refresh the token (generate a new token and update the stored server part)
        public (string cookiePart, string serverPart) RefreshToken(int accountId)
        {
            // Invalidate the current token
            InvalidateToken(accountId);
            
            // Generate a new token
            return GenerateToken(accountId);
        }
    }
}