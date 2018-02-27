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
    public class ReliableSendTests
    {
        MockSmartCallbacks _cbs;
        MockSock _bareSock;
        SmartSock _sock;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            var bufferPool = new CoreBufferPool();
            _sock = new SmartSock(bufferPool, _bareSock, _cbs);
        }

        [Test]
        public void SendReliable()
        {
            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.SendReliable(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Assert.AreEqual(1, _bareSock.Sends.Count);

            var packetToSend = _bareSock.Sends[0];
            var header = new PacketHeader();
            header.Init(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());
        }

        [Test]
        public void ReceiveReliable()
        {
            var header = new PacketHeader();
            header.SetNeedAck();
            header.SetSeqNum(123);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();

            _bareSock.FakeReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 23452));

            var receivedPacket = _sock.ReceiveFrom();

            Assert.AreEqual(1, _bareSock.Sends.Count);

            // Make sure ack sent
            var ackHeader = new PacketHeader();
            ackHeader.Init(_bareSock.Sends[0].Buffer, _bareSock.Sends[0].Offset);
            Assert.GreaterOrEqual(_bareSock.Sends[0].Buffer.Length, ackHeader.Length);
            Assert.GreaterOrEqual(_bareSock.Sends[0].Buffer.Length, ackHeader.HeaderLength);
            Assert.AreEqual(123, ackHeader.Ack);
            Assert.IsFalse(ackHeader.GetNeedAck());
            Assert.IsTrue((ackHeader.Flags & PacketHeader.ContainsAck) != 0);
        }

        [Test]
        public void ReSendAfterTimeout()
        {
            _sock.AckTimeout = 1;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.SendReliable(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Thread.Sleep(20);
            _sock.Tick();

            Assert.AreEqual(2, _bareSock.Sends.Count);
            var packetToSend = _bareSock.Sends[1];
            var header = new PacketHeader();
            header.Init(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());
        }

        [Test]
        public void NotReSendAfterAckAndTimeout()
        {
            _sock.AckTimeout = 1;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.SendReliable(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            var sent = _bareSock.Sends[0];
            var headerSent = new PacketHeader();
            headerSent.Init(sent.Buffer, sent.Offset);

            ms.Seek(0, SeekOrigin.Begin);
            var ackHeader = new PacketHeader();
            ackHeader.SetAck(headerSent.SeqNum);
            ackHeader.Length = (ushort)ackHeader.HeaderLength;
            ackHeader.WriteTo(ms);
            buffer = ms.ToArray();
            _bareSock.FakeReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 23452));

            /*var receivedPacket =*/ _sock.ReceiveFrom();

            Thread.Sleep(20);
            _sock.Tick();

            Assert.AreEqual(1, _bareSock.Sends.Count);
        }
    }
}
