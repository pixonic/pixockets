using Pixockets;
using System.Threading;

namespace TestServer
{
    class Server
    {
        static void Main(string[] args)
        {
            var callbacks = new EchoServ();
            var bufferPool = new CoreBufferPool();
            var sock = new SmartSock(bufferPool, new BareSock(bufferPool), callbacks);
            callbacks.SetSocket(sock, bufferPool);

            sock.Listen(2345);

            while (true)
            {
                Thread.Sleep(1000);

                callbacks.Tick();
            }
        }
    }
}
