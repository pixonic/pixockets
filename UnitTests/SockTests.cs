using NUnit.Framework;
using Pixockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class SockTests
    {
        [Test]
        public void SocketCreated()
        {
            MockCallbacks cbs = new MockCallbacks();
            BareSock sock = new BareSock(cbs);
        }

        [Test]
        public void SockReceive()
        {
            MockCallbacks cbs = new MockCallbacks();
            BareSock sock = new BareSock(cbs);
            sock.Receive(23456);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23456);
            udpClient.Send(BitConverter.GetBytes(123456789), 4);

            WaitOnReceive(cbs);

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(123456789, BitConverter.ToInt32(cbs.OnReceiveCalls[0].Buffer, 0));
            Assert.AreEqual(0, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(4, cbs.OnReceiveCalls[0].Length);
        }

        [Test]
        public void SockSend()
        {
            UdpClient udpClient = new UdpClient(23457);
            var receiveTask = udpClient.ReceiveAsync();

            MockCallbacks cbs = new MockCallbacks();
            BareSock sock = new BareSock(cbs);
            sock.Send(23457, BitConverter.GetBytes(123456789), 0, 4);

            receiveTask.Wait(1000);

            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
        }

        [Test]
        public void SockConnectAndReceiveFrom()
        {
            UdpClient udpClient = new UdpClient(23458);

            MockCallbacks cbs = new MockCallbacks();
            BareSock sock = new BareSock(cbs);
            sock.Connect(23458);
            sock.ReceiveFrom();

            udpClient.Send(BitConverter.GetBytes(123456789), 4, (IPEndPoint)sock.SysSock.LocalEndPoint);

            WaitOnReceive(cbs);

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(123456789, BitConverter.ToInt32(cbs.OnReceiveCalls[0].Buffer, 0));
            Assert.AreEqual(0, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(4, cbs.OnReceiveCalls[0].Length);
        }

        private static void WaitOnReceive(MockCallbacks cbs)
        {
            for (int i = 0; i < 1000; ++i)
            {
                Thread.Sleep(1);
                if (cbs.OnReceiveCalls.Count > 0)
                    break;
            }
        }
    }
}
