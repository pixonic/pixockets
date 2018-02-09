using Pixockets;
using System.Threading;

namespace ReplicatorServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var callbacks = new ReplServ();
            var bufferPool = new CoreBufferPool();
            var sock = new SmartSock(bufferPool, new BareSock(bufferPool), callbacks);
            callbacks.SetSocket(sock);

            sock.Receive(2345);

            while (true)
            {
                Thread.Sleep(100);

                callbacks.Tick();
            }
        }
    }
}
