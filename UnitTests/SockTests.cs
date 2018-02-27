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
    public class SockTests
    {
        [Test]
        public void SocketCreated()
        {
            var bufferPool = new MockBufferPool();
            var sock = new BareSock(bufferPool);
            Assert.AreEqual(0, bufferPool.Rented.Count);
            Assert.AreEqual(0, bufferPool.Returned.Count);
        }

        [Test]
        public void SockReceive()
        {
            MockCallbacks cbs = new MockCallbacks();
            var bufferPool = new MockBufferPool();
            BareSock sock = new BareSock(bufferPool);
            sock.SetCallbacks(cbs);
            sock.Receive(23456);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23456);
            udpClient.Send(BitConverter.GetBytes(123456789), 4);

            var receivedPacket = Utils.WaitOnReceive(sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(2, bufferPool.Rented.Count);
            Assert.AreEqual(0, bufferPool.Returned.Count);
        }

        [Test]
        public void SockSend()
        {
            UdpClient udpClient = new UdpClient(23457);
            var receiveTask = udpClient.ReceiveAsync();

            MockCallbacks cbs = new MockCallbacks();
            var bufferPool = new MockBufferPool();
            BareSock sock = new BareSock(bufferPool);
            sock.SetCallbacks(cbs);

            sock.Send(new IPEndPoint(IPAddress.Loopback, 23457), BitConverter.GetBytes(123456789), 0, 4, true);

            receiveTask.Wait(1000);

            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
            Assert.AreEqual(0, bufferPool.Rented.Count);
            Utils.WaitOnSet(bufferPool.Returned);
            Assert.AreEqual(1, bufferPool.Returned.Count);
            Assert.AreEqual(1, bufferPool.Alien);
        }

        [Test]
        public void SockConnectAndReceiveFrom()
        {
            UdpClient udpClient = new UdpClient(23458);

            MockCallbacks cbs = new MockCallbacks();
            var bufferPool = new MockBufferPool();
            BareSock sock = new BareSock(bufferPool);
            sock.SetCallbacks(cbs);
            sock.Connect(IPAddress.Loopback, 23458);
            sock.Receive();

            udpClient.Send(BitConverter.GetBytes(123456789), 4, (IPEndPoint)sock.SysSock.LocalEndPoint);

            var receivedPacket = Utils.WaitOnReceive(sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(2, bufferPool.Rented.Count);
            Assert.AreEqual(0, bufferPool.Returned.Count);
        }
    }
}
