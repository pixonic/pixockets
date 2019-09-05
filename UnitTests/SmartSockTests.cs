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
        public void SmartSockIgnoreReceivedPacketsBeforeConnected()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23451);
            _sock.Connect(endPoint.Address, endPoint.Port);  // NotConnected -> Connecting

            var header = new PacketHeader();
            header.SetSeqNum(1);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = Utils.ToBuffer(ms, _bufferPool);

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer.Array, buffer.Offset, buffer.Count, endPoint);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
        }

        [Test]
        public void SmartSockReceive()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23451);
            _sock.Connect(endPoint.Address, endPoint.Port);
            Utils.SendConnectResponse(_bareSock, endPoint, _bufferPool);

            var header = new PacketHeader();
            header.SetSeqNum(1);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer, 0, buffer.Length, endPoint);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket));

            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, receivedPacket.Offset));
            Assert.AreEqual(7, receivedPacket.Offset); // Length + Flags + SeqNum + SessionId
            Assert.AreEqual(4, receivedPacket.Length);
        }

        [Test]
        public void ReceivedPacketWithWrongLengthDropped()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            _sock.Connect(remoteEndPoint.Address, remoteEndPoint.Port);
            Utils.SendConnectResponse(_bareSock, remoteEndPoint, _bufferPool);

            var header = new PacketHeader();  // Wrong Length
            header.Length = 5;
            var buffer = _bufferPool.Get(header.HeaderLength);
            var ms = new MemoryStream(buffer);
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer, 0, (int)ms.Length, remoteEndPoint);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
        }

        [Test]
        public void SmartSockSendConnected()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            _sock.Connect(endPoint.Address, endPoint.Port);
            Utils.SendConnectResponse(_bareSock, endPoint, _bufferPool);
            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(buffer, 0, buffer.Length, false);

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(2, _bareSock.Sends.Count);

            var header = new PacketHeader();
            var packetToSend = _bareSock.LastSend;
            header.Init(packetToSend.Buffer, packetToSend.Offset);

            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(packetToSend.Buffer, header.HeaderLength));
            Assert.IsTrue(packetToSend.PutBufferToPool, "Unreliable packets should return to pool after send");
        }

        [Test]
        public void SmartSockSend()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            Utils.SendConnectRequest(_bareSock, endPoint, _bufferPool);
            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(endPoint, buffer, 0, buffer.Length, false);

            Assert.AreEqual(2, _bareSock.Sends.Count);

            var header = new PacketHeader();
            var packetToSend = _bareSock.LastSend;
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
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            Utils.SendConnectRequest(_bareSock, endPoint, _bufferPool);
            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(endPoint, buffer, 0, buffer.Length, false);

            Thread.Sleep(20);
            _sock.Tick();

            _sock.Send(endPoint, buffer, 0, buffer.Length, false);

            // Two sends total
            Assert.AreEqual(2, _bareSock.Sends.Count);

            // Last Sequnce number is zero
            var header = new PacketHeader();
            header.Init(_bareSock.Sends[1].Buffer, _bareSock.Sends[1].Offset);
            Assert.AreEqual(0, header.SeqNum);
        }
    }
}
