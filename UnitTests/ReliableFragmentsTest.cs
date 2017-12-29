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
    public class ReliableFragmentsTest
    {
        MockSmartCallbacks _cbs;
        MockSock _bareSock;
        SmartSock _sock;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            _sock = new SmartSock(_bareSock, _cbs);
        }

        [Test]
        public void SendReliableFragmented()
        {
            _sock.MaxPayload = 3;

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)12345), 0, 2);
            ms.Write(new byte[] { 77 }, 0, 1);
            ms.Write(BitConverter.GetBytes((ushort)23456), 0, 2);
            var buffer = ms.ToArray();
            _sock.SendReliable(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            Assert.AreEqual(2, _bareSock.Sends.Count);

            var packetToSend = _bareSock.Sends[0];
            var header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(_sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(12345, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(0, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());

            packetToSend = _bareSock.Sends[1];
            header = new PacketHeader(packetToSend.Buffer, packetToSend.Offset);
            Assert.AreEqual(buffer.Length - _sock.MaxPayload + header.HeaderLength, header.Length);
            Assert.AreEqual(23456, BitConverter.ToInt16(packetToSend.Buffer, header.HeaderLength));
            Assert.AreEqual(1, header.SeqNum);
            Assert.IsTrue(header.GetNeedAck());
        }

        [Test]
        public void ReceiveReliableFragmented()
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            var buffer1 = CreateFirstFragment();
            _bareSock.Callbacks.OnReceive(buffer1, 0, buffer1.Length, remoteEndPoint);

            var buffer2 = CreateSecondFragment();
            _bareSock.Callbacks.OnReceive(buffer2, 0, buffer2.Length, remoteEndPoint);

            AssertCombinedPacketReceived();
        }

        [Test]
        public void DuplicateReliableFragmentsIgnored()
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
        public void ReceiveSwappedReliableFragments()
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
            var header = new PacketHeader();
            header.SetNeedAck();
            header.SetSeqNum(100);
            header.SetFrag(3, 0, 2);
            header.Length = (ushort)(header.HeaderLength + 3);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes((ushort)12345), 0, 2);
            ms.Write(new byte[] { 77 }, 0, 1);
            var buffer1 = ms.ToArray();
            return buffer1;
        }

        private static byte[] CreateSecondFragment()
        {
            var header = new PacketHeader();
            header.SetNeedAck();
            header.SetSeqNum(101);
            header.SetFrag(3, 1, 2);
            header.Length = (ushort)(header.HeaderLength + 2);
            var ms = new MemoryStream();
            header.WriteTo(ms);
            ms.Write(BitConverter.GetBytes((ushort)23456), 0, 2);
            var buffer = ms.ToArray();
            return buffer;
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