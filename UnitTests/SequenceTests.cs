using NUnit.Framework;
using Pixockets;
using System;
using System.Buffers;
using System.IO;
using System.Net;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class SequenceTests
    {
        MockSmartCallbacks _cbs;
        MockSock _bareSock;
        SmartSock _sock;
        IPEndPoint _endPoint;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            _sock = new SmartSock(ArrayPool<byte>.Shared, _bareSock, _cbs);
            _endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
        }

        [Test]
        public void InOrderArrival()
        {
            SendPacket(0);
            SendPacket(1);

            Assert.AreEqual(2, _cbs.OnReceiveCalls.Count);
            Assert.IsTrue(_cbs.OnReceiveCalls[0].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[1].InOrder);
        }

        [Test]
        public void OutOfOrderArrival()
        {
            SendPacket(2);
            SendPacket(1);

            Assert.AreEqual(2, _cbs.OnReceiveCalls.Count);
            Assert.IsTrue(_cbs.OnReceiveCalls[0].InOrder);
            Assert.IsFalse(_cbs.OnReceiveCalls[1].InOrder);
        }

        [Test]
        public void CrossingEdgeOrderArrival1()
        {
            SendPacket(20000);
            SendPacket(40000);
            SendPacket(65535);
            SendPacket(0);

            Assert.AreEqual(4, _cbs.OnReceiveCalls.Count);
            Assert.IsTrue(_cbs.OnReceiveCalls[2].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[3].InOrder);
        }

        [Test]
        public void CrossingEdgeOrderArrival3()
        {
            SendPacket(20000);
            SendPacket(40000);
            SendPacket(65534);
            SendPacket(1);

            Assert.AreEqual(4, _cbs.OnReceiveCalls.Count);
            Assert.IsTrue(_cbs.OnReceiveCalls[2].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[3].InOrder);
        }

        [Test]
        public void ZigZagOrderArrival()
        {
            SendPacket(1);
            SendPacket(3);
            SendPacket(2);
            SendPacket(4);

            Assert.AreEqual(4, _cbs.OnReceiveCalls.Count);
            Assert.IsTrue(_cbs.OnReceiveCalls[0].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[1].InOrder);
            Assert.IsFalse(_cbs.OnReceiveCalls[2].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[3].InOrder);
        }

        [Test]
        public void ZigZagCrossingEdgeOrderArrival()
        {
            SendPacket(20000);
            SendPacket(40000);
            SendPacket(65534);
            SendPacket(1);
            SendPacket(65535);
            SendPacket(0);

            Assert.AreEqual(6, _cbs.OnReceiveCalls.Count);
            Assert.IsTrue(_cbs.OnReceiveCalls[0].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[1].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[2].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[3].InOrder);
            Assert.IsFalse(_cbs.OnReceiveCalls[4].InOrder);
            Assert.IsTrue(_cbs.OnReceiveCalls[5].InOrder);
        }

        private void SendPacket(int n)
        {
            var buffer = CreatePacket(n);
            _bareSock.Callbacks.OnReceive(buffer, 0, buffer.Length, _endPoint);
        }

        private static byte[] CreatePacket(int n)
        {
            var header = new PacketHeader();
            header.SetSeqNum((ushort)n);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(n), 0, 4);
            var buffer = ms.ToArray();
            return buffer;
        }
    }
}
