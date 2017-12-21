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
        [Test]
        public void SendReliable()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            sock.SendReliable(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Assert.AreEqual(1, bareSock.Sends.Count);

            var packetToSend = bareSock.Sends[0];
            var header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());
        }

        [Test]
        public void ReceiveReliable()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);

            var header = new PacketHeader();
            header.SetNeedAck();
            header.SetSeqNum(123);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();

            bareSock.Callbacks.OnReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 23452));

            Assert.AreEqual(1, bareSock.Sends.Count);

            // Make sure ack sent
            var ackHeader = new PacketHeader(bareSock.Sends[0].Buffer, bareSock.Sends[0].Offset);
            Assert.AreEqual(bareSock.Sends[0].Buffer.Length, ackHeader.Length);
            Assert.AreEqual(bareSock.Sends[0].Buffer.Length, ackHeader.HeaderLength);
            Assert.AreEqual(123, ackHeader.Ack);
            Assert.IsFalse(ackHeader.GetNeedAck());
            Assert.IsTrue((ackHeader.Flags & PacketHeader.ContainsAck) != 0);
        }

        [Test]
        public void ReSendAfterTimeout()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);
            sock.AckTimeout = 1;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            sock.SendReliable(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Thread.Sleep(20);
            sock.Tick();

            Assert.AreEqual(2, bareSock.Sends.Count);
            var packetToSend = bareSock.Sends[1];
            var header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());
        }

        [Test]
        public void NotReSendAfterAckAndTimeout()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);
            sock.AckTimeout = 1;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            sock.SendReliable(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            var sent = bareSock.Sends[0];
            var headerSent = new PacketHeader(sent.Buffer, sent.Offset);

            ms.Seek(0, SeekOrigin.Begin);
            var ackHeader = new PacketHeader();
            ackHeader.SetAck(headerSent.SeqNum);
            ackHeader.Length = (ushort)ackHeader.HeaderLength;
            ackHeader.WriteTo(ms);
            buffer = ms.ToArray();
            bareSock.Callbacks.OnReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 23452));

            Thread.Sleep(20);
            sock.Tick();

            Assert.AreEqual(1, bareSock.Sends.Count);
        }
    }
}
