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
    public class ConnectionTests
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
        public void DisconnectedOnTimeout()
        {
            _sock.ConnectionTimeout = 1;
            _sock.Connect(IPAddress.Loopback, 23451);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)9), 0, 2);  // Length
            ms.WriteByte(PacketHeader.ContainsSeq);  // Flags
            ms.Write(BitConverter.GetBytes((ushort)1), 0, 2);  // SeqNum
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.ReceiveFrom(ref receivedPacket));

            Thread.Sleep(20);
            _sock.Tick();

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _cbs.OnDisconnectCalls.Count);
        }
    }
}
