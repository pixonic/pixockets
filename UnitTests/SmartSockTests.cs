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
    public class SmartSockTests
    {
        private MockSmartCallbacks _cbs;
        private BufferPoolBase _bufferPool;
        private MockSock _bareSock;
        private SmartSock _sock;

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
        public void SmartSockReceive()
        {
            _sock.Connect(IPAddress.Loopback, 23451);

            var header = new PacketHeader(4);
            header.SetSeqNum(1);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket));

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, receivedPacket.Offset));
            Assert.AreEqual(7, receivedPacket.Offset); // Length + Flags + SeqNum + SessionId
            Assert.AreEqual(4, receivedPacket.Length);
        }

        [Test]
        public void ReceivedPacketWithWrongLengthDropped()
        {
            _sock.Connect(IPAddress.Loopback, 23451);

            var header = new PacketHeader(5);  // Wrong Length
            var buffer = _bufferPool.Get(header.Length);
            var ms = new MemoryStream(buffer);
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer, 0, (int)ms.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
        }

        [Test]
        public void SmartSockSendConnected()
        {
            _sock.Connect(IPAddress.Loopback, 23452);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(buffer, 0, buffer.Length, false);

            Assert.AreEqual(0, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _bareSock.Sends.Count);

            var header = new PacketHeader();
            var packetToSend = _bareSock.Sends[0];
            header.Init(packetToSend.Buffer, packetToSend.Offset);

            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(packetToSend.Buffer, header.HeaderLength));
            Assert.IsTrue(packetToSend.PutBufferToPool, "Unreliable packets should return to pool after send");
        }

        [Test]
        public void SmartSockSend()
        {
            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length, false);

            Assert.AreEqual(1, _bareSock.Sends.Count);

            var header = new PacketHeader();
            var packetToSend = _bareSock.Sends[0];
            header.Init(packetToSend.Buffer, packetToSend.Offset);

            Assert.AreEqual(0, header.SeqNum);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(packetToSend.Buffer, header.HeaderLength));
            Assert.IsTrue(packetToSend.PutBufferToPool, "Unreliable packets should return to pool after send");
        }

        [Test]
        public void SequenceNumberResetAfterConnectionTimeout()
        {
            _sock.ConnectionTimeout = 1;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length, false);

            Thread.Sleep(20);
            _sock.Tick();
            Assert.AreEqual(0, _cbs.OnConnectCalls.Count);

            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length, false);

            // Two sends total
            Assert.AreEqual(2, _bareSock.Sends.Count);

            // Last Sequnce number is zero
            var header = new PacketHeader();
            header.Init(_bareSock.Sends[1].Buffer, _bareSock.Sends[1].Offset);
            Assert.AreEqual(0, header.SeqNum);
        }
    }
}
