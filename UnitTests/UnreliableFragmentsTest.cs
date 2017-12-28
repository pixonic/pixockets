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
    public class UnreliableFragmentsTest
    {
        MockCallbacks _cbs;
        MockSock _bareSock;
        SmartSock _sock;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockCallbacks();
            _bareSock = new MockSock();
            _sock = new SmartSock(_bareSock, _cbs);
        }

        [Test]
        public void SendFragmented()
        {
            _sock.MaxPayload = 3;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)12345), 0, 2);
            ms.Write(new byte[] { 77 }, 0, 1);
            ms.Write(BitConverter.GetBytes((ushort)23456), 0, 2);
            var buffer = ms.ToArray();
            _sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Assert.AreEqual(2, _bareSock.Sends.Count);

            var packetToSend = _bareSock.Sends[0];
            var header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(_sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(12345, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsFalse(header.GetNeedAck());

            packetToSend = _bareSock.Sends[1];
            header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(buffer.Length - _sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(23456, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(1, header.SeqNum);
            Assert.IsFalse(header.GetNeedAck());
        }

        [Test]
        public void ReceiveFragmented()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var buffer1 = CreateFirstFragment();
            _bareSock.Callbacks.OnReceive(buffer1, 0, buffer1.Length, remoteEndPoint);

            var buffer2 = CreateSecondFragment();
            _bareSock.Callbacks.OnReceive(buffer2, 0, buffer2.Length, remoteEndPoint);

            AssertCombinedPacketReceived();
        }

        [Test]
        public void DuplicateFragmentsIgnored()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var buffer1 = CreateFirstFragment();
            _bareSock.Callbacks.OnReceive(buffer1, 0, buffer1.Length, remoteEndPoint);
            // Duplicate
            _bareSock.Callbacks.OnReceive(buffer1, 0, buffer1.Length, remoteEndPoint);

            var buffer2 = CreateSecondFragment();
            _bareSock.Callbacks.OnReceive(buffer2, 0, buffer2.Length, remoteEndPoint);

            AssertCombinedPacketReceived();
        }

        [Test]
        public void ReceiveSwappedFragments()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var buffer2 = CreateSecondFragment();
            _bareSock.Callbacks.OnReceive(buffer2, 0, buffer2.Length, remoteEndPoint);

            var buffer1 = CreateFirstFragment();
            _bareSock.Callbacks.OnReceive(buffer1, 0, buffer1.Length, remoteEndPoint);

            AssertCombinedPacketReceived();
        }

        [Test]
        public void ReceiveFailOnTimeoutBetweenFragments()
        {
            _sock.FragmentTimeout = 1;

            byte[] buffer1 = CreateFirstFragment();

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            _bareSock.Callbacks.OnReceive(buffer1, 0, buffer1.Length, remoteEndPoint);

            byte[] buffer2 = CreateSecondFragment();

            Thread.Sleep(20);
            _sock.Tick();

            _bareSock.Callbacks.OnReceive(buffer2, 0, buffer2.Length, remoteEndPoint);

            // Make sure nothing received
            Assert.AreEqual(0, _cbs.OnReceiveCalls.Count);
        }

        private static byte[] CreateFirstFragment()
        {
            var header1 = new PacketHeader();
            header1.SetSeqNum(100);
            header1.SetFrag(3, 0, 2);
            header1.Length = (ushort)(header1.HeaderLength + 3);
            var ms1 = new MemoryStream();
            header1.WriteTo(ms1);
            ms1.Write(BitConverter.GetBytes((ushort)12345), 0, 2);
            ms1.Write(new byte[] { 77 }, 0, 1);
            var buffer1 = ms1.ToArray();
            return buffer1;
        }

        private static byte[] CreateSecondFragment()
        {
            var header2 = new PacketHeader();
            header2.SetSeqNum(101);
            header2.SetFrag(3, 1, 2);
            header2.Length = (ushort)(header2.HeaderLength + 2);
            var ms2 = new MemoryStream();
            header2.WriteTo(ms2);
            ms2.Write(BitConverter.GetBytes((ushort)23456), 0, 2);
            var buffer2 = ms2.ToArray();
            return buffer2;
        }

        private void AssertCombinedPacketReceived()
        {
            // Make sure full combined packet received
            Assert.AreEqual(1, _cbs.OnReceiveCalls.Count);
            Assert.AreEqual(12345, BitConverter.ToUInt16(_cbs.OnReceiveCalls[0].Buffer, _cbs.OnReceiveCalls[0].Offset));
            Assert.AreEqual(77, _cbs.OnReceiveCalls[0].Buffer[_cbs.OnReceiveCalls[0].Offset + 2]);
            Assert.AreEqual(23456, BitConverter.ToUInt16(_cbs.OnReceiveCalls[0].Buffer, _cbs.OnReceiveCalls[0].Offset + 3));
            Assert.AreEqual(5, _cbs.OnReceiveCalls[0].Length);
        }
    }
}
