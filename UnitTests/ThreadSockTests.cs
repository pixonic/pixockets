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
    public class ThreadSockTests
    {
        private MockBufferPool _bufferPool;
        private ThreadSock _sock;

        [SetUp]
        public void Setup()
        {
            _bufferPool = new MockBufferPool();
            _sock = new ThreadSock(_bufferPool, AddressFamily.InterNetwork, new LoggerStub());
        }

        [TearDown]
        public void TearDown()
        {
            _sock.Close();
        }

        [Test]
        public void ThreadSocketCreated()
        {
            Assert.AreEqual(0, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
        }

        [Test]
        public void ThreadSockReceive()
        {
            _sock.Listen(23476);
            Assert.AreEqual(IPAddress.Any, _sock.LocalEndPoint.Address);
            Assert.AreEqual(23476, _sock.LocalEndPoint.Port);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23476);
            udpClient.Send(BitConverter.GetBytes(123456789), 4);

            var receivedPacket = Utils.WaitOnReceive(_sock);

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, 0));
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(4, receivedPacket.Length);
            Assert.AreEqual(1, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
        }

        [Test]
        public void ThreadSockSend()
        {
            UdpClient udpClient = new UdpClient(23477);
            var receiveTask = udpClient.ReceiveAsync();

            // Send to specified EndPoint, don't put buffer to pool
            var sendEP = new IPEndPoint(IPAddress.Loopback, 23477);
            _sock.Send(sendEP, BitConverter.GetBytes(123456789), 0, 4, false);

            receiveTask.Wait(1000);

            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
            Assert.AreEqual(0, _bufferPool.Rented.Count);
            Assert.AreEqual(0, _bufferPool.Returned.Count);
            Assert.AreEqual(0, _bufferPool.Alien);
        }

        [Test]
        public void ThreadSockSendConnected()
        {
            UdpClient udpClient = new UdpClient(23479);
            var sendEP = new IPEndPoint(IPAddress.Loopback, 23479);
            var receiveTask = udpClient.ReceiveAsync();

            // Send to already connected EndPoint, get buffer from pool
            _sock.Connect(sendEP.Address, sendEP.Port);
            var buf = _bufferPool.Get(4);
            Array.Copy(BitConverter.GetBytes(123456789), buf, 4);
            _sock.Send(buf, 0, 4, true);

            receiveTask.Wait(1000);

            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
            Assert.AreEqual(1, _bufferPool.Rented.Count);
            Utils.WaitOnSet(_bufferPool.Returned);
            Assert.AreEqual(1, _bufferPool.Returned.Count);
            Assert.AreEqual(0, _bufferPool.Alien);
        }

        [Test]
        public void ThreadSockConnectAndReceiveFrom()
        {
            UdpClient udpClient = new UdpClient(23478);

            _sock.Connect(IPAddress.Loopback, 23478);

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
