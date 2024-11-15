using System;

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
            string pw = reader.ReadMmoString();
            int port = reader.ReadInt32();
            string level = reader.ReadMmoString();
            string zone = reader.ReadMmoString();
            if (Server!.Settings.ServerPassword == pw)
            {
#if DEBUG
                Console.WriteLine($"{DateTime.Now:HH:mm} Server logged in. Map: {level}, IP: 127.0.0.1 (due to DEBUG), Port: {port}");
#else
                Console.WriteLine($"{DateTime.Now:HH:mm} Server logged in. Map: {level}, IP: {connection.Ip}, Port: {port}");
#endif
                Server!.Processor.ConQ.Enqueue(async () => await ProcessLoginServer(port, level, zone, connection));
            }
            else
            {
                Console.WriteLine($"Refusing server login: wrong password. Server tried: {pw}");
                _ = connection.Disconnect(); // not awaited
            }
        }

        private async Task ProcessLoginServer(int port, string level, string zone, UserConnection conn)
        {
            GameServer newServer = new GameServer(port, level, zone, conn);
            Server!.GameLogic.ServerConnected(conn, newServer);

            // retreive all persistent objects
            var persistentObjects = await Server!.Database.GetPersistentObjects(port, level);

            // retreive the server's serialized data and send it back to server
            string? serverInfo = await Server!.Database.GetServerInfo(port, level);

            Console.WriteLine("Server logged in, assigning Guid: " + newServer.Conn.Id.ToString());
            // send the login message
            byte[] loginMsg = MergeByteArrays(ToBytes(RpcType.RpcLoginServer), WriteMmoString(newServer.Conn.Id.ToString()), WriteMmoString(serverInfo ?? ""), ToBytes(persistentObjects.Count)) ;
            conn.Send(loginMsg);            

            // send all persistent objects in DB to the server identified by its port & level            
            foreach (var obj in persistentObjects)
            {
                byte[] msg = MergeByteArrays(ToBytes(RpcType.RpcSavePersistentObject), ToBytes(obj.ObjectId), WriteMmoString(obj.JsonString));
                conn.Send(msg);
            }
        }
    }
}