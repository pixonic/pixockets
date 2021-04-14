using NUnit.Framework;
using Pixockets;
using System;
using System.IO;
using System.Net;
using System.Threading;
using Pixockets.Pools;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class ConnectionTests
    {
        private MockSmartCallbacks _cbs;
        private MockSock _bareSock;
        private SmartSock _sock;
        private BufferPoolBase _bufferPool;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            _bufferPool = new ByteBufferPool();
            _sock = new SmartSock(_bufferPool, _bareSock, _cbs);
        }

        [TearDown]
        public void TearDown()
        {
            _sock.Close();
        }

        [Test]
        public void ConnectRequestResent()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            _sock.ConnectRequestResendPeriod = 0;
            _sock.Connect(remoteEndPoint.Address, remoteEndPoint.Port);

            Assert.AreEqual(1, _bareSock.Sends.Count);

            for (int i = 2; i < 12; ++i)
            {
                _sock.Tick();
                Assert.AreEqual(i, _bareSock.Sends.Count);
            }
        }

        [Test]
        public void ConnectResponseSent()
        {
            Assert.AreEqual(PixocketState.NotConnected, _sock.State);

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            Utils.SendConnectRequest(_bareSock, remoteEndPoint, _bufferPool);

            Assert.AreEqual(PixocketState.NotConnected, _sock.State, "Server is not going to connected state");

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            Assert.AreEqual(PixocketState.NotConnected, _sock.State, "Server is not going to connected state");

            Assert.AreEqual(1, _bareSock.Sends.Count);
            var header = new PacketHeader();
            var packetSent = _bareSock.LastSend;
            header.Init(packetSent.Buffer, packetSent.Offset);
            Assert.IsTrue((header.Flags & PacketHeader.Connect) != 0);
            Assert.AreNotEqual(PacketHeader.EmptySessionId, header.SessionId);

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(remoteEndPoint, _cbs.OnConnectCalls[0]);
            Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);
        }

        [Test]
        public void ConnectResponseResent()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);

            var receivedPacket = new ReceivedSmartPacket();
            for (int i = 1; i < 11; ++i)
            {
                Utils.SendConnectRequest(_bareSock, remoteEndPoint, _bufferPool);

                Assert.AreEqual(PixocketState.NotConnected, _sock.State, "Server is not going to connected state");

                Assert.IsFalse(_sock.Receive(ref receivedPacket));

                Assert.AreEqual(PixocketState.NotConnected, _sock.State, "Server is not going to connected state");

                Assert.AreEqual(i, _bareSock.Sends.Count);
                var header = new PacketHeader();
                var packetSent = _bareSock.LastSend;
                header.Init(packetSent.Buffer, packetSent.Offset);
                Assert.IsTrue((header.Flags & PacketHeader.Connect) != 0);
                Assert.AreNotEqual(PacketHeader.EmptySessionId, header.SessionId);

                Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
                Assert.AreEqual(remoteEndPoint, _cbs.OnConnectCalls[0]);
                Assert.AreEqual(0, _cbs.OnDisconnectCalls.Count);
            }
        }

        [Test]
        public void DisconnectedOnTimeout()
        {
            _sock.ConnectionTimeout = 1;

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            _sock.Connect(remoteEndPoint.Address, remoteEndPoint.Port);
            Utils.SendConnectResponse(_bareSock, remoteEndPoint, _bufferPool);

            var header = new PacketHeader();
            header.SetSeqNum(1);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = Utils.ToBuffer(ms, _bufferPool);

            // Simulate send from UdpClient
            _bareSock.FakeReceive(buffer.Array, buffer.Offset, buffer.Count, remoteEndPoint);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket));

            Thread.Sleep(20);
            _sock.Tick();

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _cbs.OnDisconnectCalls.Count);
            Assert.AreEqual(DisconnectReason.Timeout, _cbs.OnDisconnectCalls[0].Item2);
        }

        [Test]
        public void DisconnectedRequestSent()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            _sock.Connect(remoteEndPoint.Address, remoteEndPoint.Port);
            Utils.SendConnectResponse(_bareSock, remoteEndPoint, _bufferPool);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
            // Now we are connected

            _sock.Disconnect(remoteEndPoint);
            Utils.SendDisconnectResponse(_bareSock, remoteEndPoint, _bufferPool);
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
            // Now we are disconnected

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _cbs.OnDisconnectCalls.Count);
            Assert.AreEqual(DisconnectReason.InitiatedByPeer, _cbs.OnDisconnectCalls[0].Item2);
        }

        [Test]
        public void DisconnectedRequestReceived()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            _sock.Connect(remoteEndPoint.Address, remoteEndPoint.Port);
            Utils.SendConnectResponse(_bareSock, remoteEndPoint, _bufferPool);

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
            // Now we are connected

            Assert.AreEqual(1, _bareSock.Sends.Count);

            Utils.SendDisconnectRequest(_bareSock, remoteEndPoint, _bufferPool);
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
            // Now we are disconnected

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _cbs.OnDisconnectCalls.Count);
            Assert.AreEqual(DisconnectReason.InitiatedByPeer, _cbs.OnDisconnectCalls[0].Item2);
            Assert.AreEqual(2, _bareSock.Sends.Count);
        }
    }
}
