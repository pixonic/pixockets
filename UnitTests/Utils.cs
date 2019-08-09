using System;
using Pixockets;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnitTests.Mock;

namespace UnitTests
{
    class Utils
    {
        public static ReceivedPacket WaitOnReceive(SockBase sock)
        {
            var receivedPacket = new ReceivedPacket();
            for (int i = 0; i < 1000; ++i)
            {
                if (sock.Receive(ref receivedPacket))
                {
                    return receivedPacket;
                }
                Thread.Sleep(1);
            }
            return receivedPacket;
        }

        public static ReceivedSmartPacket WaitOnReceive(SmartSock sock)
        {
            for (int i = 0; i < 1000; ++i)
            {
                var receivedPacket = new ReceivedSmartPacket();
                if (sock.Receive(ref receivedPacket))
                {
                    return receivedPacket;
                }
                Thread.Sleep(1);
            }
            return new ReceivedSmartPacket();
        }

        public static List<ReceivedSmartPacket> ReceiveAll(SmartSock sock)
        {
            var result = new List<ReceivedSmartPacket>();
            for (int i = 0; i < 1000; ++i)
            {
                var receivedPacket = new ReceivedSmartPacket();
                if (sock.Receive(ref receivedPacket))
                {
                    result.Add(receivedPacket);
                }
                else
                {
                    break;
                }
            }
            return result;
        }

        public static void WaitOnList<T>(List<T> list)
        {
            for (int i = 0; i < 1000; ++i)
            {
                Thread.Sleep(1);
                if (list.Count > 0)
                    break;
            }
        }

        public static void WaitOnSet<T>(HashSet<T> set)
        {
            for (int i = 0; i < 1000; ++i)
            {
                Thread.Sleep(1);
                if (set.Count > 0)
                    break;
            }
        }

        public static ArraySegment<byte> ToBuffer(MemoryStream ms, BufferPoolBase bufferPool)
        {
            var buffer = bufferPool.Get((int)ms.Length);
            var array = ms.ToArray();
            Array.Copy(array, buffer, array.Length);
            return new ArraySegment<byte>(buffer, 0, array.Length);
        }
    }
}
