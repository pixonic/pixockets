using Pixockets;
using System.Buffers;
using System.Threading;

namespace ReplicatorServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var callbacks = new ReplServ();
            var sock = new SmartSock(ArrayPool<byte>.Shared, new BareSock(ArrayPool<byte>.Shared), callbacks);
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
