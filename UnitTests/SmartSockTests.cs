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
        public void SmartSockReceive()
        {
            _sock.Connect(IPAddress.Loopback, 23451);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)9), 0, 2);  // Length
            ms.WriteByte(PacketHeader.ContainsSeq);  // Flags
            ms.Write(BitConverter.GetBytes((ushort)1), 0, 2); // SeqNum
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.ReceiveFrom(ref receivedPacket));
                
            Assert.AreEqual(123456789, BitConverter.ToInt32(receivedPacket.Buffer, receivedPacket.Offset));
            Assert.AreEqual(5, receivedPacket.Offset); // Length + Flags + SeqNum
            Assert.AreEqual(4, receivedPacket.Length);
        }

        [Test]
        public void ReceivedPacketWithWrongLengthDropped()
        {
            _sock.Connect(IPAddress.Loopback, 23451);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)5), 0, 2);  // Wrong Length
            ms.WriteByte(0);  // Flags
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.ReceiveFrom(ref receivedPacket));

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
        }

        [Test]
        public void SmartSockSendConnected()
        {
            _sock.Connect(IPAddress.Loopback, 23452);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(buffer, 0, buffer.Length);

            Assert.AreEqual(0, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _bareSock.Sends.Count);

            var header = new PacketHeader();
            header.Init(_bareSock.Sends[0].Buffer, _bareSock.Sends[0].Offset);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(_bareSock.Sends[0].Buffer, header.HeaderLength));
        }

        [Test]
        public void SmartSockSend()
        {
            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);
            
            Assert.AreEqual(1, _bareSock.Sends.Count);

            var header = new PacketHeader();
            header.Init(_bareSock.Sends[0].Buffer, _bareSock.Sends[0].Offset);
            Assert.AreEqual(0, header.SeqNum);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(_bareSock.Sends[0].Buffer, header.HeaderLength));
        }

        [Test]
        public void SequenceNumberResetAfterConnectionTimeout()
        {
            _sock.ConnectionTimeout = 1;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Thread.Sleep(20);
            _sock.Tick();
            Assert.AreEqual(0, _cbs.OnConnectCalls.Count);

            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            // Two sends total
            Assert.AreEqual(2, _bareSock.Sends.Count);

            // Last Sequnce number is zero
            var header = new PacketHeader();
            header.Init(_bareSock.Sends[1].Buffer, _bareSock.Sends[1].Offset);
            Assert.AreEqual(0, header.SeqNum);
        }
    }
}
