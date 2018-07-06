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
    public class ReliableFragmentsTest
    {
        MockSmartCallbacks _cbs;
        CoreBufferPool _bufferPool;
        MockSock _bareSock;
        SmartSock _sock;

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
        public void SendReliableFragmented()
        {
            _sock.MaxPayload = 3;

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)12345), 0, 2);
            ms.Write(new byte[] { 77 }, 0, 1);
            ms.Write(BitConverter.GetBytes((ushort)23456), 0, 2);
            var buffer = ms.ToArray();
            _sock.Send(remoteEndPoint, buffer, 0, buffer.Length, true);

            Assert.AreEqual(2, _bareSock.Sends.Count);

            var packetToSend = _bareSock.Sends[0];
            var header = new PacketHeader();
            header.Init(packetToSend.Buffer, packetToSend.Offset);

            Assert.AreEqual(_sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(12345, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());
            Assert.IsFalse(packetToSend.PutBufferToPool, "Reliable packets should wait for Ack before going to pool");

            packetToSend = _bareSock.Sends[1];
            header = new PacketHeader();
            header.Init(packetToSend.Buffer, packetToSend.Offset);

            Assert.AreEqual(buffer.Length - _sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(23456, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(1, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());
            Assert.IsFalse(packetToSend.PutBufferToPool, "Reliable packets should wait for Ack before going to pool");

            // Ack buffers
            var header1 = new PacketHeader();
            header1.AddAck(0);
            header1.AddAck(1);
            header1.SetSeqNum(1);
            header1.Length = (ushort)header1.HeaderLength;
            var buffer1 = _bufferPool.Get(header1.Length);
            header1.WriteTo(buffer1, 0);

            _bareSock.FakeReceive(buffer1, 0, header1.Length, remoteEndPoint);

            var receivedPackets = Utils.ReceiveAll(_sock);
        }

        [Test]
        public void ReceiveReliableFragmented()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var buffer1 = CreateFirstFragment();
            _bareSock.FakeReceive(buffer1.Array, buffer1.Offset, buffer1.Count, remoteEndPoint);

            var buffer2 = CreateSecondFragment();
            _bareSock.FakeReceive(buffer2.Array, buffer2.Offset, buffer2.Count, remoteEndPoint);

            AssertCombinedPacketReceived();
        }

        [Test]
        public void DuplicateReliableFragmentsIgnored()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var buffer1 = CreateFirstFragment();
            _bareSock.FakeReceive(buffer1.Array, buffer1.Offset, buffer1.Count, remoteEndPoint);
            // Duplicate
            _bareSock.FakeReceive(buffer1.Array, buffer1.Offset, buffer1.Count, remoteEndPoint);

            var buffer2 = CreateSecondFragment();
            _bareSock.FakeReceive(buffer2.Array, buffer1.Offset, buffer2.Count, remoteEndPoint);

            AssertCombinedPacketReceived();
        }

        [Test]
        public void ReceiveSwappedReliableFragments()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var buffer2 = CreateSecondFragment();
            _bareSock.FakeReceive(buffer2.Array, buffer2.Offset, buffer2.Count, remoteEndPoint);

            var buffer1 = CreateFirstFragment();
            _bareSock.FakeReceive(buffer1.Array, buffer1.Offset, buffer1.Count, remoteEndPoint);

            AssertCombinedPacketReceived();
        }

        [Test]
        public void ReceiveReliableFailOnTimeoutBetweenFragments()
        {
            _sock.FragmentTimeout = 1;

            var buffer1 = CreateFirstFragment();

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            _bareSock.FakeReceive(buffer1.Array, buffer1.Offset, buffer1.Count, remoteEndPoint);
            var receivedPacket = new ReceivedSmartPacket();
            Assert.IsFalse(_sock.Receive(ref receivedPacket));

            Thread.Sleep(20);
            _sock.Tick();

            var buffer2 = CreateSecondFragment();
            _bareSock.FakeReceive(buffer2.Array, buffer2.Offset, buffer2.Count, remoteEndPoint);

            // Make sure nothing received
            Assert.IsFalse(_sock.Receive(ref receivedPacket));
        }

        private ArraySegment<byte> CreateFirstFragment()
        {
            var header = new PacketHeader();
            header.SetNeedAck();
            header.SetSeqNum(100);
            header.SetFrag(3, 0, 2);
            header.Length = (ushort)(header.HeaderLength + 3);
            var buffer = _bufferPool.Get(header.Length);
            header.WriteTo(buffer, 0);
            Array.Copy(BitConverter.GetBytes((ushort)12345), 0, buffer, header.HeaderLength, 2);
            buffer[header.HeaderLength + 2] = 77;

            return new ArraySegment<byte>(buffer, 0, header.Length);
        }

        private ArraySegment<byte> CreateSecondFragment()
        {
            var header = new PacketHeader();
            header.SetNeedAck();
            header.SetSeqNum(101);
            header.SetFrag(3, 1, 2);
            header.Length = (ushort)(header.HeaderLength + 2);
            var buffer = _bufferPool.Get(header.Length);
            header.WriteTo(buffer, 0);
            Array.Copy(BitConverter.GetBytes((ushort)23456), 0, buffer, header.HeaderLength, 2);

            return new ArraySegment<byte>(buffer, 0, header.Length);
        }

        private void AssertCombinedPacketReceived()
        {
            // Make sure full combined packet received
            var receivedPackets = Utils.ReceiveAll(_sock);
            Assert.AreEqual(1, receivedPackets.Count);
            Assert.AreEqual(12345, BitConverter.ToUInt16(receivedPackets[0].Buffer, receivedPackets[0].Offset));
            Assert.AreEqual(77, receivedPackets[0].Buffer[receivedPackets[0].Offset + 2]);
            Assert.AreEqual(23456, BitConverter.ToUInt16(receivedPackets[0].Buffer, receivedPackets[0].Offset + 3));
            Assert.AreEqual(5, receivedPackets[0].Length);
        }
    }
}
