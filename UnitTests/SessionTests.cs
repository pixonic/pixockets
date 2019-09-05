using NUnit.Framework;
using Pixockets;
using System;
using System.IO;
using System.Net;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class SessionTests
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
        public void ClientInitiallySendsEmptySessionIdAndConnectFlag()
        {
            // Connect Request
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            _sock.Connect(endPoint.Address, endPoint.Port);

            Assert.AreEqual(1, _bareSock.Sends.Count);
            Assert.AreEqual(0, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);
            var packet = _bareSock.LastSend;
            var header = new PacketHeader();
            header.Init(packet.Buffer, packet.Offset);

            Assert.AreEqual(PacketHeader.EmptySessionId, header.SessionId);
            Assert.IsTrue((header.Flags & PacketHeader.Connect) != 0);

            // Connect Response
            Utils.SendConnectResponse(_bareSock, endPoint, _bufferPool);
            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);

            // TODO: should it work?
            _sock.Send(BitConverter.GetBytes(123456789), 0, 4, false);

            Assert.AreEqual(2, _bareSock.Sends.Count);
            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);

            packet = _bareSock.LastSend;
            header = new PacketHeader();
            header.Init(packet.Buffer, packet.Offset);

            Assert.AreEqual(427, header.SessionId);
        }

        [Test]
        public void ServerRepliesWithFilledSessionId()
        {
            var clientEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            Utils.SendConnectRequest(_bareSock, clientEndPoint, _bufferPool);

            ushort seqNum = 1;
            FakeSendPacket(0, 123456789, clientEndPoint, ref seqNum);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket));
            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);

            // Reply from server
            _sock.Send(clientEndPoint, BitConverter.GetBytes(123456789), 0, 4, false);

            Assert.AreEqual(2, _bareSock.Sends.Count);

            var packet = _bareSock.LastSend;
            var header = new PacketHeader();
            header.Init(packet.Buffer, packet.Offset);

            Assert.AreNotEqual(PacketHeader.EmptySessionId, header.SessionId);
        }

        [Test]
        public void ServerDidNotConnectWhenClientSessionIdIsNotEmpty()
        {
            var clientEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);

            ushort seqNum = 1;
            FakeSendPacket(123, 123456789, clientEndPoint, ref seqNum);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
            Assert.AreEqual(0, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);
        }

        [Test]
        public void ServerDropPacketWithChangedSessionId()
        {
            var clientEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            Utils.SendConnectRequest(_bareSock, clientEndPoint, _bufferPool);

            ushort seqNum = 1;
            FakeSendPacket(0, 123456789, clientEndPoint, ref seqNum);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket));
            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);

            // Reply from server
            _sock.Send(clientEndPoint, BitConverter.GetBytes(123456789), 0, 4, false);

            Assert.AreEqual(2, _bareSock.Sends.Count);

            var packet = _bareSock.LastSend;
            var header = new PacketHeader();
            header.Init(packet.Buffer, packet.Offset);

            Assert.AreNotEqual(PacketHeader.EmptySessionId, header.SessionId);

            ushort otherSessionId = (ushort)(header.SessionId > 30000 ? 12345 : 45678);
            FakeSendPacket(otherSessionId, 123456789, clientEndPoint, ref seqNum);

            Assert.IsFalse(_sock.Receive(ref receivedPacket));
            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);  // Only initial Connect
            Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);
        }

        [Test]
        public void ClientImbuesSessionId()
        {
            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, 23451);
            _sock.Connect(serverEndPoint.Address, serverEndPoint.Port);

            _sock.Send(BitConverter.GetBytes(123456789), 0, 4, false);
            ushort seqNum = 1;
            FakeSendPacket(223, 1234567, serverEndPoint, ref seqNum);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket));

            _sock.Send(BitConverter.GetBytes(123456789), 0, 4, false);

            var packet = _bareSock.LastSend;
            var header2 = new PacketHeader();
            header2.Init(packet.Buffer, packet.Offset);

            Assert.AreEqual(223, header2.SessionId);
        }

        private ArraySegment<byte> ToBuffer(MemoryStream ms)
        {
            return Utils.ToBuffer(ms, _bufferPool);
        }

        private void FakeSendPacket(ushort sessionId, int payload, IPEndPoint serverEndPoint, ref ushort seqNum)
        {
            var header = new PacketHeader();
            header.SetSessionId(sessionId);
            header.SetSeqNum(seqNum++);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(payload), 0, 4);
            var buffer = Utils.ToBuffer(ms, _bufferPool);

            _bareSock.FakeReceive(buffer.Array, buffer.Offset, buffer.Count, serverEndPoint);
        }
    }
}
