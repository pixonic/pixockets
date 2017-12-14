using Pixockets;
using System;

namespace TestServer
{
    class Server
    {
        static void Main(string[] args)
        {
            var callbacks = new EchoServ();
            var sock = new BareSock(callbacks);
            callbacks.SetSocket(sock);

            sock.Receive(2345);

            Console.Read();
        }
    }
}
