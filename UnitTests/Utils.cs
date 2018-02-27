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
            for (int i = 0; i < 1000; ++i)
            {
                var receivedPacket = sock.ReceiveFrom();
                if (receivedPacket != null)
                {
                    return receivedPacket;
                }
                Thread.Sleep(1);
            }
            return null;
        }

        public static ReceivedSmartPacket WaitOnReceive(SmartSock sock)
        {
            for (int i = 0; i < 1000; ++i)
            {
                var receivedPacket = sock.ReceiveFrom();
                if (receivedPacket != null)
                {
                    return receivedPacket;
                }
                Thread.Sleep(1);
            }
            return null;
        }

        public static List<ReceivedSmartPacket> ReceiveAll(SmartSock sock)
        {
            var result = new List<ReceivedSmartPacket>();
            for (int i = 0; i < 1000; ++i)
            {
                var receivedPacket = sock.ReceiveFrom();
                if (receivedPacket != null)
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
