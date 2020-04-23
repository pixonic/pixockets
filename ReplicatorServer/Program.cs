using System.Net.Sockets;
using Pixockets;
using System.Threading;
using Pixockets.DebugTools;
using Pixockets.Pools;

namespace ReplicatorServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var callbacks = new ReplServ();
            var bufferPool = new ByteBufferPool();
            var sock = new SmartSock(bufferPool, new BareSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub()), callbacks);
            callbacks.SetSocket(sock);

            sock.Listen(2345);

            while (true)
            {
                Thread.Sleep(100);

                callbacks.Tick();
            }
        }
    }
}
