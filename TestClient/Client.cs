using Pixockets;
using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Threading;

namespace TestClient
{
    class Client
    {
        static void Main(string[] args)
        {
            var callbacks = new PrintingReceiver();
            var sock = new SmartSock(ArrayPool<byte>.Shared, new BareSock(ArrayPool<byte>.Shared), callbacks);

            sock.Connect(IPAddress.Loopback, 2345);
            sock.Receive();

            {
                var rnd = new Random(Guid.NewGuid().GetHashCode());
                var count = 700 + rnd.Next(500);
                var ms = new MemoryStream(4 + count * 4);
                ms.Write(BitConverter.GetBytes(count), 0, 4);
                for (int i = 0; i < count; ++i)
                {
                    ms.Write(BitConverter.GetBytes(i), 0, 4);
                }

                var initMsg = ms.ToArray();
                sock.Send(initMsg, 0, initMsg.Length);
            }

            Thread.Sleep(1000);

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
