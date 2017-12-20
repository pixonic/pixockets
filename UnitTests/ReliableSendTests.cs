using NUnit.Framework;
using Pixockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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

            var header = new PacketHeader(bareSock.Sends[0].Buffer, bareSock.Sends[0].Offset);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(bareSock.Sends[0].Buffer, header.HeaderLength));
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

            var ackHeader = new PacketHeader(bareSock.Sends[0].Buffer, bareSock.Sends[0].Offset);
            Assert.AreEqual(bareSock.Sends[0].Buffer.Length, ackHeader.Length);
            Assert.AreEqual(bareSock.Sends[0].Buffer.Length, ackHeader.HeaderLength);
            Assert.AreEqual(123, ackHeader.Ack);
            Assert.IsFalse(ackHeader.GetNeedAck());
            Assert.IsTrue((ackHeader.Flags & PacketHeader.ContainsAck) != 0);
        }
    }
}
