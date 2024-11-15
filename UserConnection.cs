using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace PersistenceServer
{
    public class UserConnection : TcpSession
    {
        private readonly MmoTcpServer _mmoTcpServer;

        public UserConnection(TcpServer server) : base(server) {
            _mmoTcpServer = (MmoTcpServer)server;
        }

        protected override void OnConnected()
        {            
            // Dummy reader
            BinaryReader reader = new BinaryReader(new MemoryStream(new byte[] { }));
            _mmoTcpServer.InvokeOnMessageReceived(RpcType.RpcConnected, this, reader);
        }

        protected override void OnDisconnected()
        {            
            // Dummy reader
            BinaryReader reader = new BinaryReader(new MemoryStream(new byte[] { }));
            _mmoTcpServer.InvokeOnMessageReceived(RpcType.RpcDisconnected, this, reader);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Read the first byte to determine the RpcType we received and send it to all RpcReaders
            Stream stream = new MemoryStream(buffer, (int)offset, (int)size);
            BinaryReader reader = new BinaryReader(stream, Encoding.ASCII); // in ASCII, a char is equal one byte, so PeekChar will peek for one byte. In UTF, it throws an exception (very rarely)
            while (reader.PeekChar() > 0) // -1 is "end of stream", "0" is "undefined"
            {
                RpcType rpcPrefix = (RpcType)reader.ReadByte();
                _mmoTcpServer.InvokeOnMessageReceived(rpcPrefix, this, reader);
            }

            //string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            //Console.WriteLine($"(thread {Thread.CurrentThread.ManagedThreadId}) Incoming: {message}");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"TCP session caught an error with code {error}");
        }
    }
}
