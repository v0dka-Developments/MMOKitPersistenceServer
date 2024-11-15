namespace PersistenceServer.RPCs
{
    public class Disconnected : BaseRpc
    {
        public Disconnected()
        {
            RpcType = RpcType.RpcDisconnected; // set it to the RpcType you want to catch
        }

        // Read message from the reader, then enqueue an Action on the concurrent queue server.Processor.ConQ
        // For example: Server!.Processor.ConQ.Enqueue(() => Console.WriteLine("like this"));
        protected override void ReadRpc(UserConnection connection, BinaryReader reader)
        {
#if DEBUG
            Console.Write($"(thread {Thread.CurrentThread.ManagedThreadId}) ");
#endif
            Console.WriteLine($"User disconnected. Session Id: {connection.Id}");

            Server!.Processor.ConQ.Enqueue(() => Server!.GameLogic.UserDisconnected(connection));
        }
    }
}