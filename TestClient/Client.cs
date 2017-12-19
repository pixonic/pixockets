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
            var sock = new BareSock();
            sock.SetCallbacks(callbacks);

            sock.Connect(IPAddress.Loopback, 2345);
            sock.Receive();

            while (true)
            {
                var msg = Console.ReadLine();

                var buffer = Encoding.UTF8.GetBytes(msg);
                sock.Send(buffer, 0, buffer.Length);
            }
        }
    }
}
