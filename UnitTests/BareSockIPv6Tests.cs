using NUnit.Framework;
using Pixockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Pixockets.DebugTools;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class BareSockIPv6Tests
    {
        private MockBufferPool _bufferPool;
        private BareSock _sock;

        [SetUp]
        public void Setup()
        {
            _bufferPool = new MockBufferPool();
            _sock = new BareSock(_bufferPool, AddressFamily.InterNetworkV6, new LoggerStub());
        }

        [TearDown]
        public void TearDown()
        {
            _sock.Close();
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
            _sock.Listen(23446);

            UdpClient udpClient = new UdpClient(AddressFamily.InterNetworkV6);
            udpClient.Connect(IPAddress.IPv6Loopback, 23446);
            udpClient.Send(BitConverter.GetBytes(123456789), 4);

            var receivedPacket = Utils.WaitOnReceive(_sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(1, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
        }

        [Test]
        public void SockSend()
        {
            UdpClient udpClient = new UdpClient(23447, AddressFamily.InterNetworkV6);
            var receiveTask = udpClient.ReceiveAsync();

            _sock.Send(new IPEndPoint(IPAddress.IPv6Loopback, 23447), BitConverter.GetBytes(123456789), 0, 4, true);

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
            UdpClient udpClient = new UdpClient(23448, AddressFamily.InterNetworkV6);

            _sock.Connect(IPAddress.IPv6Loopback, 23448);

            udpClient.Send(BitConverter.GetBytes(123456789), 4, (IPEndPoint)_sock.SysSock.LocalEndPoint);

            var receivedPacket = Utils.WaitOnReceive(_sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(1, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
        }
    }
}
