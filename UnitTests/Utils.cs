using Pixockets;
using System.Collections.Generic;
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
                if (sock.ReceiveFrom(ref receivedPacket))
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
                if (sock.ReceiveFrom(ref receivedPacket))
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
                if (sock.ReceiveFrom(ref receivedPacket))
                {
                    result.Add(receivedPacket);
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
    }
}
