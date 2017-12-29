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
        [Test]
        public void SmartSockReceive()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);
            sock.Connect(IPAddress.Loopback, 23451);
            sock.Receive();

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)7), 0, 2);  // Length
            ms.WriteByte(0);  // Flags
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            bareSock.Callbacks.OnReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);            
            Assert.AreEqual(123456789, BitConverter.ToInt32(cbs.OnReceiveCalls[0].Buffer, cbs.OnReceiveCalls[0].Offset));
            Assert.AreEqual(PacketHeader.MinHeaderLength, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(4, cbs.OnReceiveCalls[0].Length);
        }

        [Test]
        public void ReceivedPacketWithWrongLengthDropped()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);
            sock.Connect(IPAddress.Loopback, 23451);
            sock.Receive();

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)5), 0, 2);  // Wrong Length
            ms.WriteByte(0);  // Flags
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            bareSock.Callbacks.OnReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            Assert.AreEqual(0, cbs.OnReceiveCalls.Count);
        }

        [Test]
        public void SmartSockSendConnected()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);
            sock.Connect(IPAddress.Loopback, 23452);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            sock.Send(buffer, 0, buffer.Length);

            Assert.AreEqual(1, bareSock.Sends.Count);

            var header = new PacketHeader(bareSock.Sends[0].Buffer, bareSock.Sends[0].Offset);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(bareSock.Sends[0].Buffer, header.HeaderLength));
        }

        [Test]
        public void SmartSockSend()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);
            
            Assert.AreEqual(1, bareSock.Sends.Count);

            var header = new PacketHeader(bareSock.Sends[0].Buffer, bareSock.Sends[0].Offset);
            Assert.AreEqual(0, header.SeqNum);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(bareSock.Sends[0].Buffer, header.HeaderLength));
        }

        [Test]
        public void SequenceNumberResetAfterConnectionTimeout()
        {
            var cbs = new MockCallbacks();
            var bareSock = new MockSock();
            var sock = new SmartSock(bareSock, cbs);
            sock.ConnectionTimeout = 1;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Thread.Sleep(20);
            sock.Tick();

            sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            // Two sends total
            Assert.AreEqual(2, bareSock.Sends.Count);

            // Last Sequnce number is zero
            var header = new PacketHeader(bareSock.Sends[1].Buffer, bareSock.Sends[1].Offset);
            Assert.AreEqual(0, header.SeqNum);
        }
    }
}
