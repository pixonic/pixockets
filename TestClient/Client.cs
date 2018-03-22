using Pixockets;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TestClient
{
    class Client
    {
        static void Main(string[] args)
        {
            var callbacks = new PrintingReceiver();
            var bufferPool = new CoreBufferPool();
            var sock = new SmartSock(bufferPool, new BareSock(bufferPool, AddressFamily.InterNetwork), callbacks);
            var address = args[0];

            callbacks.Connecting = true;
            Connect(address, sock);

            Thread.Sleep(1000);

            int cnt = 0;
            while (true)
            {
                Thread.Sleep(1000);

                cnt++;
                if (cnt > 32)
                {
                    cnt = 1;
                }

                var buffer = CreateMessage(cnt);
                sock.Send(buffer, 0, buffer.Length, false);
                sock.Tick();
                var packet = new ReceivedSmartPacket();
                while (true)
                {
                    if (sock.Receive(ref packet))
                    {
                        callbacks.OnReceive(packet.Buffer, packet.Offset, packet.Length, packet.EndPoint, packet.InOrder);
                    }
                    else
                    {
                        break;
                    }
                }

                if (!callbacks.Connected && !callbacks.Connecting)
                {
                    callbacks.Connecting = true;
                    Connect(address, sock);
                    continue;
                }
            }
        }

        private static void Connect(string addr, SmartSock sock)
        {
            sock.Connect(IPAddress.Parse(addr), 2345);

            var ep = (IPEndPoint)sock.LocalEndPoint;
            Console.WriteLine("{0}:{1}", ep.Address, ep.Port);

            var rnd = new Random(Guid.NewGuid().GetHashCode());
            var count = 700 + rnd.Next(500);
            byte[] initMsg = CreateMessage(count);
            Console.WriteLine("Sending message with {0} numbers", count);
            sock.Send(initMsg, 0, initMsg.Length, true);
        }

        private static byte[] CreateMessage(int count)
        {
            var ms = new MemoryStream(4 + count * 4);
            ms.Write(BitConverter.GetBytes(count), 0, 4);
            for (int i = 0; i < count; ++i)
            {
                ms.Write(BitConverter.GetBytes(i), 0, 4);
            }

            var initMsg = ms.ToArray();
            return initMsg;
        }
    }
}
