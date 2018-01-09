using Pixockets;
using System;
using System.Threading;

namespace TestServer
{
    class Server
    {
        static void Main(string[] args)
        {
            var callbacks = new EchoServ();
            var sock = new SmartSock(new BareSock(), callbacks);
            callbacks.SetSocket(sock);

            sock.Receive(2345);

            while (true)
            {
                Thread.Sleep(1000);

                callbacks.Tick();
            }
        }
    }
}
