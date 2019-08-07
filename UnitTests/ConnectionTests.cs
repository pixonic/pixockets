using NUnit.Framework;
using Pixockets;
using System;
using System.IO;
using System.Net;
using System.Threading;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class ConnectionTests
    {
        private MockSmartCallbacks _cbs;
        private MockSock _bareSock;
        private SmartSock _sock;
        private BufferPoolBase _bufferPool;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            _bufferPool = new CoreBufferPool();
            _sock = new SmartSock(_bufferPool, _bareSock, _cbs);
        }

        [TearDown]
        public void TearDown()
        {
            _sock.Close();
        }

        [Test]
        public void DisconnectedOnTimeout()
        {
            _sock.ConnectionTimeout = 1;
            _sock.Connect(IPAddress.Loopback, 23451);

            var header = new PacketHeader();
            header.SetSeqNum(1);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = Utils.ToBuffer(ms, _bufferPool);

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer.Array, buffer.Offset, buffer.Count, new IPEndPoint(IPAddress.Loopback, 54321));

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket));

            Thread.Sleep(20);
            _sock.Tick();

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _cbs.OnDisconnectCalls.Count);
        }
    }
}
