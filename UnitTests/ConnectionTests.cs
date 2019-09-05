﻿using NUnit.Framework;
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
        private MockSmartCallbacks _cbs;
        private MockSock _bareSock;
        private SmartSock _sock;
        private BufferPoolBase _bufferPool;

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
        public void ConnectResponseSent()
        {
            Assert.AreEqual(PixockState.NotConnected, _sock.State);

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 54321);
            Utils.SendConnectRequest(_bareSock, remoteEndPoint, _bufferPool);

            Assert.AreEqual(PixockState.NotConnected, _sock.State, "Server is not going to connected state");

            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            Assert.AreEqual(PixockState.NotConnected, _sock.State, "Server is not going to connected state");

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
        }
    }
}
