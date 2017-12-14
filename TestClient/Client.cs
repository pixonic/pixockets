using Pixockets;
using System;
using System.Text;

namespace TestClient
{
    class Client
    {
        static void Main(string[] args)
        {
            var callbacks = new PrintingReceiver();
            var sock = new BareSock(callbacks);

            sock.Connect(2345);
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
