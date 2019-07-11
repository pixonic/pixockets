﻿using System.Net.Sockets;
using Pixockets;
using System.Threading;
using Pixockets.Debug;

namespace TestServer
{
    class Server
    {
        static void Main(string[] args)
        {
            var callbacks = new EchoServ();
            var bufferPool = new CoreBufferPool();
            var sock = new SmartSock(bufferPool, new BareSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub()), callbacks);
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
