using System.Net;
using NUnit.Framework;
using Pixockets;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class InternalPacketsArePutBackToPoolTests
    {
        private MockSmartCallbacks _cbs;
        private MockBufferPool _bufferPool;
        private MockSock _bareSock;
        private SmartSock _sock;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            _bufferPool = new MockBufferPool();
            _sock = new SmartSock(_bufferPool, _bareSock, _cbs);
        }

        [TearDown]
        public void TearDown()
        {
            _sock.Close();
        }

        [Test]
        public void AcksPacketIsPutToPool()
        {
            var header = new PacketHeader();
            header.AddAck(1);
            header.AddAck(2);
            header.AddAck(3);

            TestWith(header);
        }

        [Test]
        public void WrongLengthPacketIsPutToPool()
        {
            var header = new PacketHeader(3);

            TestWith(header);
        }

        [Test]
        public void EmptyPacketIsPutToPool()
        {
            var header = new PacketHeader();

            TestWith(header);
        }

        private void TestWith(PacketHeader header)
        {
            header.Length = (ushort)header.HeaderLength;

            var buffer = _bufferPool.Get(header.Length);
            header.WriteTo(buffer, 0);

            _bareSock.FakeReceive(buffer, 0, header.Length, new IPEndPoint(IPAddress.Loopback, 23452));

            Utils.ReceiveAll(_sock);

            Assert.AreEqual(1, _bufferPool.Rented.Count);
            Assert.AreEqual(1, _bufferPool.Returned.Count);
            Assert.AreEqual(0, _bufferPool.Alien);
        }
    }
}
