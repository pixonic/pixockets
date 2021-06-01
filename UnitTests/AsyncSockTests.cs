using NUnit.Framework;
using Pixockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnitTests.Mock;
using NSubstitute;
using Pixockets.DebugTools;

namespace UnitTests
{
    [TestFixture]
    public class AsyncSockTests
    {
        private MockBufferPool _bufferPool;
        private AsyncSock _sock;
        private ILogger _logger;


        [SetUp]
        public void Setup()
        {
            _logger = Substitute.For<ILogger>();
            _bufferPool = new MockBufferPool();
            _sock = new AsyncSock(_bufferPool, AddressFamily.InterNetwork, _logger);
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
            _sock.Listen(33456);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 33456);
            udpClient.Send(BitConverter.GetBytes(123456789), 4);

            var receivedPacket = Utils.WaitOnReceive(_sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(1, _bufferPool.Rented.Count);
            Assert.AreEqual(1, _bufferPool.Returned.Count);
        }

        [Test]
        public void SockSend()
        {
            UdpClient udpClient = new UdpClient(33457);
            var receiveTask = udpClient.ReceiveAsync();

            _sock.Send(new IPEndPoint(IPAddress.Loopback, 33457), BitConverter.GetBytes(123456789), 0, 4, true);

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
            UdpClient udpClient = new UdpClient(33458);

            _sock.Connect(IPAddress.Loopback, 33458);
            _sock.Send(BitConverter.GetBytes(123456789), 0, 4, false);
            IPEndPoint localEndPoint = null;
            udpClient.Receive(ref localEndPoint);

            udpClient.Send(BitConverter.GetBytes(123456789), 4, localEndPoint);

            var receivedPacket = Utils.WaitOnReceive(_sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(1, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count, "Buffer should not be returned to pool until processed by client");
        }

        [Test]
        public void SendToOnConnectedModeTest()
        {
            UdpClient udpClient = new UdpClient(33455);
            var receiveTask = udpClient.ReceiveAsync();
            _sock.Connect(IPAddress.Loopback, 33455);
            _sock.Send(new IPEndPoint(IPAddress.Loopback, 33455), BitConverter.GetBytes(123456789), 0, 4, true);

            receiveTask.Wait(1000);
            _logger.DidNotReceiveWithAnyArgs().Exception(null);
            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
            Assert.AreEqual(0, _bufferPool.Rented.Count);
            Utils.WaitOnSet(_bufferPool.Returned);
            Assert.AreEqual(1, _bufferPool.Returned.Count);
            Assert.AreEqual(1, _bufferPool.Alien);
        }
    }
}
