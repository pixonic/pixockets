using Pixockets;
using System;
using System.Net;
using System.Text;

namespace TestClient
{
    class Client
    {
        static void Main(string[] args)
        {
            var callbacks = new PrintingReceiver();
            var sock = new BareSock(callbacks);

            sock.Connect(IPAddress.Loopback, 2345);
            sock.ReceiveFrom();

            while (true)
            {
                var msg = Console.ReadLine();

                var buffer = Encoding.UTF8.GetBytes(msg);
                sock.SendTo(buffer, 0, buffer.Length);
            }
        }
    }
}
