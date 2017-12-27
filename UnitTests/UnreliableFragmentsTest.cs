using NUnit.Framework;
using Pixockets;
using System;
using System.IO;
using System.Net;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class UnreliableFragmentsTest
    {
        [Test]
        public void SendFragmented()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);
            sock.MaxPayload = 3;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)12345), 0, 2);
            ms.Write(new byte[] { 77 }, 0, 1);
            ms.Write(BitConverter.GetBytes((ushort)23456), 0, 2);
            var buffer = ms.ToArray();
            sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Assert.AreEqual(2, bareSock.Sends.Count);

            var packetToSend = bareSock.Sends[0];
            var header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(12345, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsFalse(header.GetNeedAck());

            packetToSend = bareSock.Sends[1];
            header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(buffer.Length - sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(23456, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(1, header.SeqNum);
            Assert.IsFalse(header.GetNeedAck());
        }

        [Test]
        public void ReceiveFragmented()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);

            var header1 = new PacketHeader();
            header1.SetSeqNum(100);
            header1.SetFrag(3, 0, 2);
            header1.Length = (ushort)(header1.HeaderLength + 3);
            var ms1 = new MemoryStream();
            header1.WriteTo(ms1);
            ms1.Write(BitConverter.GetBytes((ushort)12345), 0, 2);
            ms1.Write(new byte[] { 77 }, 0, 1);
            var buffer1 = ms1.ToArray();

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            bareSock.Callbacks.OnReceive(buffer1, 0, buffer1.Length, remoteEndPoint);

            var header2 = new PacketHeader();
            header2.SetSeqNum(101);
            header2.SetFrag(3, 1, 2);
            header2.Length = (ushort)(header2.HeaderLength + 2);
            var ms2 = new MemoryStream();
            header2.WriteTo(ms2);
            ms2.Write(BitConverter.GetBytes((ushort)23456), 0, 2);
            var buffer2 = ms2.ToArray();

            bareSock.Callbacks.OnReceive(buffer2, 0, buffer2.Length, remoteEndPoint);

            //sock.Tick();

            // Make sure full packet combined sent
            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(12345, BitConverter.ToUInt16(cbs.OnReceiveCalls[0].Buffer, cbs.OnReceiveCalls[0].Offset));
            Assert.AreEqual(77, cbs.OnReceiveCalls[0].Buffer[cbs.OnReceiveCalls[0].Offset+2]);
            Assert.AreEqual(23456, BitConverter.ToUInt16(cbs.OnReceiveCalls[0].Buffer, cbs.OnReceiveCalls[0].Offset+3));
            Assert.AreEqual(5, cbs.OnReceiveCalls[0].Length);
        }
    }
}
