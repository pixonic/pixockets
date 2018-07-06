﻿using NUnit.Framework;
using Pixockets;
using System;
using System.IO;
using System.Net;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class SequenceTests
    {
        MockSmartCallbacks _cbs;
        MockSock _bareSock;
        SmartSock _sock;
        IPEndPoint _endPoint;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            var bufferPool = new CoreBufferPool();
            _sock = new SmartSock(bufferPool, _bareSock, _cbs);
            _endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
        }

        [TearDown]
        public void TearDown()
        {
            _sock.Close();
        }

        [Test]
        public void InOrderArrival()
        {
            SendPacket(0);
            SendPacket(1);

            var receivedPacket1 = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket1));
            Assert.IsTrue(receivedPacket1.InOrder);

            var receivedPacket2 = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket2));
            Assert.IsTrue(receivedPacket2.InOrder);
        }

        [Test]
        public void OutOfOrderArrival()
        {
            SendPacket(2);
            SendPacket(1);

            var receivedPacket1 = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket1));
            Assert.IsTrue(receivedPacket1.InOrder);

            var receivedPacket2 = new ReceivedSmartPacket();
            Assert.IsTrue(_sock.Receive(ref receivedPacket2));
            Assert.IsFalse(receivedPacket2.InOrder);
        }

        [Test]
        public void CrossingEdgeOrderArrival1()
        {
            SendPacket(20000);
            SendPacket(40000);
            SendPacket(65535);
            SendPacket(0);

            var receivedPackets = Utils.ReceiveAll(_sock);

            Assert.AreEqual(4, receivedPackets.Count);
            Assert.IsTrue(receivedPackets[2].InOrder);
            Assert.IsTrue(receivedPackets[3].InOrder);
        }

        [Test]
        public void CrossingEdgeOrderArrival3()
        {
            SendPacket(20000);
            SendPacket(40000);
            SendPacket(65534);
            SendPacket(1);

            var receivedPackets = Utils.ReceiveAll(_sock);

            Assert.AreEqual(4, receivedPackets.Count);
            Assert.IsTrue(receivedPackets[2].InOrder);
            Assert.IsTrue(receivedPackets[3].InOrder);
        }

        [Test]
        public void ZigZagOrderArrival()
        {
            SendPacket(1);
            SendPacket(3);
            SendPacket(2);
            SendPacket(4);

            var receivedPackets = Utils.ReceiveAll(_sock);

            Assert.AreEqual(4, receivedPackets.Count);
            Assert.IsTrue(receivedPackets[0].InOrder);
            Assert.IsTrue(receivedPackets[1].InOrder);
            Assert.IsFalse(receivedPackets[2].InOrder);
            Assert.IsTrue(receivedPackets[3].InOrder);
        }

        [Test]
        public void ZigZagCrossingEdgeOrderArrival()
        {
            SendPacket(20000);
            SendPacket(40000);
            SendPacket(65535);
            SendPacket(0);
            SendPacket(65534);
            SendPacket(1);

            var receivedPackets = Utils.ReceiveAll(_sock);

            Assert.AreEqual(6, receivedPackets.Count);
            Assert.IsTrue(receivedPackets[0].InOrder);
            Assert.IsTrue(receivedPackets[1].InOrder);
            Assert.IsTrue(receivedPackets[2].InOrder);
            Assert.IsTrue(receivedPackets[3].InOrder);
            Assert.IsFalse(receivedPackets[4].InOrder);
            Assert.IsTrue(receivedPackets[5].InOrder);
        }

        [Test]
        public void ResetOrderArrival()
        {
            SendPacket(40000);
            SendPacket(40001);

            var receivedPackets = Utils.ReceiveAll(_sock);

            Assert.AreEqual(2, receivedPackets.Count);
            Assert.IsTrue(receivedPackets[0].InOrder);
            Assert.IsTrue(receivedPackets[1].InOrder);
        }

        [Test]
        public void HugeGapOrderArrival()
        {
            SendPacket(1);
            SendPacket(65000);

            var receivedPackets = Utils.ReceiveAll(_sock);

            Assert.AreEqual(2, receivedPackets.Count);
            Assert.IsTrue(receivedPackets[0].InOrder);
            Assert.IsFalse(receivedPackets[1].InOrder);
        }

        [Test]
        public void SerialDuplicatesDetected()
        {
            SendPacket(1);
            SendPacket(1);
            var receivedPackets = Utils.ReceiveAll(_sock);
            Assert.AreEqual(1, receivedPackets.Count);

            SendPacket(1);
            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
        }

        [Test]
        public void InterleavedDuplicatesDetected()
        {
            SendPacket(1);
            SendPacket(2);

            var receivedPackets = Utils.ReceiveAll(_sock);
            Assert.AreEqual(2, receivedPackets.Count);

            SendPacket(1);
            var receivedPacket1 = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket1));

            SendPacket(2);
            var receivedPacket2 = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket2));
        }

        private void SendPacket(int n)
        {
            var buffer = CreatePacket(n);
            _bareSock.FakeReceive(buffer, 0, buffer.Length, _endPoint);
        }

        private static byte[] CreatePacket(int n)
        {
            var header = new PacketHeader();
            header.SetSeqNum((ushort)n);
            header.Length = (ushort)(header.HeaderLength + 4);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes(n), 0, 4);
            var buffer = ms.ToArray();
            return buffer;
        }
    }
}
