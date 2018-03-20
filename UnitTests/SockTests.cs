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
        private MockBufferPool _bufferPool;
        private BareSock _sock;


        [SetUp]
        public void Setup()
        {
            _bufferPool = new MockBufferPool();
            _sock = new BareSock(_bufferPool);
        }

        [Test]
        public void SocketCreated()
        {
            Assert.AreEqual(0, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
        }

        [Test]
        public void SockReceive()
        {
            _sock.Receive(23456);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23456);
            udpClient.Send(BitConverter.GetBytes(123456789), 4);

            var receivedPacket = Utils.WaitOnReceive(_sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(2, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
        }

        [Test]
        public void SockSend()
        {
            UdpClient udpClient = new UdpClient(23457);
            var receiveTask = udpClient.ReceiveAsync();

            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23457), BitConverter.GetBytes(123456789), 0, 4, true);

            receiveTask.Wait(1000);

            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
            Assert.AreEqual(0, _bufferPool.Rented.Count);
            Utils.WaitOnSet(_bufferPool.Returned);
            Assert.AreEqual(1, _bufferPool.Returned.Count);
            Assert.AreEqual(1, _bufferPool.Alien);
        }

        [Test]
        public void SockConnectAndReceiveFrom()
        {
            UdpClient udpClient = new UdpClient(23458);

            _sock.Connect(IPAddress.Loopback, 23458);

            udpClient.Send(BitConverter.GetBytes(123456789), 4, (IPEndPoint)_sock.SysSock.LocalEndPoint);

            var receivedPacket = Utils.WaitOnReceive(_sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(2, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
        }
    }
}
