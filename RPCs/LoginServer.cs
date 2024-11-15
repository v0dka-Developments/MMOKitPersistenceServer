namespace PersistenceServer.RPCs
{
    public class LoginServer : BaseRpc
    {
        public LoginServer()
        {
            RpcType = RpcType.RpcLoginServer; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        // Look at other RPCs for more examples.
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
            var pw = reader.ReadMmoString();
            if (Server!.Settings.ServerPassword == pw)
            {
                Console.WriteLine("Server logged in!");
                Server!.Processor.ConQ.Enqueue(() => Server!.GameLogic.ServerConnected(connection));
            }
            else
            {
                Console.WriteLine($"Refusing server login: wrong password. Server tried: {pw}");
                connection.Disconnect();
            }
        }
    }
}