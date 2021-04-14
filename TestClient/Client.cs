using Pixockets;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Pixockets.DebugTools;
using Pixockets.Pools;

namespace TestClient
{
    class Client
    {
        static void Main(string[] args)
        {
            var callbacks = new PrintingReceiver();
            var bufferPool = new ByteBufferPool();
            var sock = new SmartSock(bufferPool, new BareSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub()), callbacks);
            var address = args[0];

            callbacks.Connecting = true;
            Connect(address, sock);
            var packet = new ReceivedSmartPacket();
            while (sock.State == PixocketState.Connecting)
            {
                if (sock.Receive(ref packet))
                    break;
                sock.Tick();
            }

            for (int i = 1; i < 11; i++)
            {
                Thread.Sleep(1000);

                var buffer = CreateMessage(i);
                sock.Send(buffer, 0, buffer.Length, false);
                sock.Tick();
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

            sock.Disconnect();

            while (sock.State == PixocketState.Connected)
            {
                sock.Tick();
                sock.Receive(ref packet);
                Thread.Sleep(100);
            }
        }

        private static void Connect(string addr, SmartSock sock)
        {
            sock.Connect(IPAddress.Parse(addr), 2345);

            var ep = sock.LocalEndPoint;
            Console.WriteLine("{0}:{1}", ep.Address, ep.Port);

            var packet = new ReceivedSmartPacket();
            while (sock.State == PixocketState.Connecting)
            {
                sock.Tick();
                if (sock.Receive(ref packet))
                    break;
            }

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
