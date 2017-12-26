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
        public void HappySendFragmented()
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
    }
}
