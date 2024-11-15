using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PersistenceServer
{
    public class MmoWsServer
    {        
        public readonly ActionsSyncher Processor;
        public readonly GameLogic GameLogic;
        public readonly Database Database;
        public readonly SettingsReader Settings;

        public delegate void MessageReceivedHandler(RpcType inRpcType, UserConnection conn, BinaryReader reader);
        public event MessageReceivedHandler? OnMessageReceived;
        private readonly IWebHost? host;

        public static MmoWsServer? Singleton; // for usage from HttpControllers

        public MmoWsServer(SettingsReader inSettings, Database inDatabase)
        {
            Singleton = this;

            Settings = inSettings;
            // Contains a list of Players, Guilds, Parties, etc
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

            // Build WS server
            host = new WebHostBuilder()
            .UseKestrel(options =>
            {
                options.Listen(IPAddress.Any, inSettings.Port);
            })
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers().AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null; // prevents using the default camel-case policy, which would change the field names from uppercase to lowercase for no reason
                });
            })
            .Configure(app =>
            {
                app.UseWebSockets();
                app.UseRouting();

                // Middleware for WebSockets
                app.UseEndpoints(endpoints =>
                {
                    endpoints.Map("/mmo", async context =>
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                            // The following part is needed for UE5 server instances. We need to know their IP to redirect the players to their address.
                            // By default, we try to use remote IP address, which is going to work if PS and UE servers run on different machines and don't share a local network.
                            // If they run on the same machine, we're going to return Settings.PersistenceServerIP to clients.
                            // Here's a special case: if the PS is running on Machine1 and the UE server is running on Machine2 and they share a local network,
                            // then we can't determine the real IP of the UE server that just connected. We only know the server's local IP.
                            // The latter setup will still work if the clients connect also from the same local network.
                            // However, if the developper attempts to connect to a local UE5 server from outside of the local network, it'll fail.
                            // It's a special case related to home testing and local networks.                            

                            IPAddress ip = context.Connection.RemoteIpAddress!;
                            Console.WriteLine($"Connection from: {ip}");

                            // if the IP is from a local network
                            if (ip.ToString().StartsWith("127.") || ip.ToString().StartsWith("192.168."))
                            {
                                // Not sure how well this works, it's up to you to figure out your network setup, developers...
                                if (context.Connection.LocalIpAddress!.ToString() == "127.0.0.1" || context.Connection.LocalIpAddress == GetLocalIPAddress())
                                {
                                    ip = IPAddress.Parse(Settings.PersistenceServerIP);
                                }
                            }
                            //// Uncomment to debug:
                            //if (context.Connection.RemoteIpAddress != null) Console.WriteLine($"Connection's remote ip: {context.Connection.RemoteIpAddress}");
                            //if (context.Connection.LocalIpAddress != null) Console.WriteLine($"Connection's local ip: {context.Connection.LocalIpAddress}");
                            var userConnection = new UserConnection(this, webSocket, ip);
                            await userConnection.HandleConnectionAsync();
                        }
                        else
                        {
                            Console.WriteLine("Rejecting connection with code 400");
                            context.Response.StatusCode = 400;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"error\": \"400 Bad Request - Only WebSocket connections are accepted on this endpoint\"}");
                        }
                    });
                });

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

                // Middleware to handle 404 responses
                app.Use(async (context, next) =>
                {
                    await next();
                    if (context.Response.StatusCode == 404)
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"error\": \"404 Endpoint not found\"}");
                    }
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders(); // Removes all log providers, including the console one.
                logging.AddFilter("Microsoft", LogLevel.Warning) // Log only warnings or errors from Microsoft libraries
                       .AddFilter("System", LogLevel.Warning); // Log only warnings or errors from System libraries
            })
            .Build();
        }

        public void Start()
        {
            host?.Start();
        }

        public async Task Stop()
        {
            if (host != null)
                await host.StopAsync();
        }

        public void InvokeOnMessageReceived(RpcType inRpcType, UserConnection conn, BinaryReader reader)
        {
            /// This will call ReadRpc() on all BaseRPC subclasses that are of the correct RpcType, <see cref="BaseRPC"/>
            OnMessageReceived?.Invoke(inRpcType, conn, reader);
        }

        public void BroadcastAdminMessage(string msg)
        {
            byte[] msgBytes = BaseRpc.WriteMmoString(msg);
            BinaryReader reader = new(new MemoryStream(msgBytes));
            // since we're sending it from the console and not from an actual ue5 server, we have to use a little "hack" by finding a random ue5 server connection
            // if we don't find it, it means there are no servers and therefore no players online, and so we skip broadcasting the message
            if (GameLogic.GetAllServerConnections().Length > 0)
                InvokeOnMessageReceived(RpcType.RpcAdminMessage, GameLogic.GetAllServerConnections()[0], reader);
            else
                Console.WriteLine("No connected servers and players to send a message to.");
        }

        public async Task<int> RequestGuilds()
        {
            var guilds = await Database.GetGuilds();
            GameLogic.AssignGuilds(guilds);
            return guilds.Count;
        }

        // Gets the IP address of this machine on the local network
        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}
