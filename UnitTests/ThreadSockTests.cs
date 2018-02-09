using NUnit.Framework;
using Pixockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class ThreadSockTests
    {
        [Test]
        public void ThreadSocketCreated()
        {
            var bufferPool = new CoreBufferPool();
            var sock = new ThreadSock(bufferPool);
        }

        [Test]
        public void ThreadSockReceive()
        {
            MockCallbacks cbs = new MockCallbacks();
            var bufferPool = new CoreBufferPool();
            var sock = new ThreadSock(bufferPool);
            sock.SetCallbacks(cbs);
            sock.Receive(23466);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23466);
            udpClient.Send(BitConverter.GetBytes(123456789), 4);

            Utils.WaitOnReceive(cbs);

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(123456789, BitConverter.ToInt32(cbs.OnReceiveCalls[0].Buffer, 0));
            Assert.AreEqual(0, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(4, cbs.OnReceiveCalls[0].Length);
        }

        [Test]
        public void ThreadSockSend()
        {
            UdpClient udpClient = new UdpClient(23467);
            var receiveTask = udpClient.ReceiveAsync();

            MockCallbacks cbs = new MockCallbacks();
            var bufferPool = new CoreBufferPool();
            var sock = new ThreadSock(bufferPool);
            sock.SetCallbacks(cbs);

            sock.Send(new IPEndPoint(IPAddress.Loopback, 23467), BitConverter.GetBytes(123456789), 0, 4, true);

            receiveTask.Wait(1000);

            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
        }

        [Test]
        public void ThreadSockConnectAndReceiveFrom()
        {
            UdpClient udpClient = new UdpClient(23468);

            MockCallbacks cbs = new MockCallbacks();
            var bufferPool = new CoreBufferPool();
            var sock = new ThreadSock(bufferPool);
            sock.SetCallbacks(cbs);
            sock.Connect(IPAddress.Loopback, 23468);
            sock.Receive();

            udpClient.Send(BitConverter.GetBytes(123456789), 4, (IPEndPoint)sock.SysSock.LocalEndPoint);

            Utils.WaitOnReceive(cbs);

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(123456789, BitConverter.ToInt32(cbs.OnReceiveCalls[0].Buffer, 0));
            Assert.AreEqual(0, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(4, cbs.OnReceiveCalls[0].Length);
        }
    }
}
