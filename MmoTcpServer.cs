using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;

namespace PersistenceServer
{
    public class MmoTcpServer : TcpServer
    {        
        public readonly ActionsSyncher Processor;
        public readonly GameLogic GameLogic;
        public readonly Database Database;
        public readonly SettingsReader Settings;
        public delegate void MessageReceivedHandler(RpcType inRpcType, UserConnection conn, BinaryReader reader);
        public event MessageReceivedHandler? OnMessageReceived;

        public MmoTcpServer(IPAddress address, SettingsReader inSettings, Database inDatabase) : base(address, inSettings.Port) 
        {
            Settings = inSettings;
            // Contains a list of Players, Groups, etc
            GameLogic = new();
            // Launch a Thread that will 'Tick' every 8ms and process Actions on a Concurrent Queue
            Processor = new();
            _ = Processor.Tick();

            Database = inDatabase;

            // Create an instance of each class that inherits from BaseRPC
            List<BaseRpc> rpcReaders = typeof(BaseRpc).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BaseRpc))).Select(t => (BaseRpc)Activator.CreateInstance(t)!).ToList();
#if DEBUG
            // Print all found readers
            var readerNames = rpcReaders.Select(rpcReader => rpcReader.GetType().ToString().Replace("PersistenceServer.", "")).ToArray();
            Console.WriteLine($"Found RPC readers: {rpcReaders.Count} [{string.Join(", ", readerNames)}]");
#endif
            // Check that their rpc type is set - the check only happens in Debug            
            foreach (var reader in rpcReaders) Debug.Assert(reader.RpcType != RpcType.RpcUndef);

            // Subscribe each class to OnMessageReceived event
            foreach (var reader in rpcReaders) reader.SubscribeToMessages(this);
        }

        public void InvokeOnMessageReceived(RpcType inRpcType, UserConnection conn, BinaryReader reader)
        {
            /// This will call ReadRpc() on all BaseRPC subclasses that are of the correct RpcType, <see cref="BaseRPC"/>
            OnMessageReceived?.Invoke(inRpcType, conn, reader);
        }

        protected override TcpSession CreateSession() { return new UserConnection(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"MMO Kit server caught an error with code {error}");
        }

        public void BroadcastAdminMessage(string msg)
        {
            Multicast(msg);
        }
    }
}
