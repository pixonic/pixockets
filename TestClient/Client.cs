using Pixockets;
using System;
using System.Net;
using System.Threading;

namespace TestClient
{
    class Client
    {
        static void Main(string[] args)
        {
            var callbacks = new PrintingReceiver();
            var sock = new SmartSock(new BareSock(), callbacks);

            sock.Connect(IPAddress.Loopback, 2345);
            sock.Receive();

            while (true)
            {
                Thread.Sleep(1000);

                sock.Tick();

                var msg = Environment.TickCount;

                var buffer = BitConverter.GetBytes(msg);
                sock.Send(buffer, 0, buffer.Length);
            }
        }
    }
}
