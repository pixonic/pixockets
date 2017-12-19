﻿using NUnit.Framework;
using Pixockets;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class SmartSockTests
    {
        [Test]
        public void SmartSockReceive()
        {
            UdpClient udpClient = new UdpClient(23451);            

            var cbs = new MockCallbacks();
            var sock = new SmartSock(new BareSock(), cbs);
            sock.Connect(IPAddress.Loopback, 23451);
            sock.Receive();

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)7), 0, 2);
            ms.WriteByte(0);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            udpClient.Send(buffer, buffer.Length, sock.LocalEndPoint);

            Utils.WaitOnReceive(cbs);

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(123456789, BitConverter.ToInt32(cbs.OnReceiveCalls[0].Buffer, PacketHeader.MinHeaderLength));
            Assert.AreEqual(PacketHeader.MinHeaderLength, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(4, cbs.OnReceiveCalls[0].Length);
        }

        [Test]
        public void SmartSockSend()
        {
            UdpClient udpClient = new UdpClient(23452);
            var receiveTask = udpClient.ReceiveAsync();

            var cbs = new MockCallbacks();
            var sock = new SmartSock(new BareSock(), cbs);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            sock.Send(new IPEndPoint(IPAddress.Loopback, 23452), buffer, 0, buffer.Length);

            receiveTask.Wait(1000);

            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);

            var header = new PacketHeader(receiveTask.Result.Buffer, 0);
            Assert.AreEqual(buffer.Length + header.HeaderLength, header.Length);
            Assert.AreEqual(123456789, BitConverter.ToInt32(receiveTask.Result.Buffer, PacketHeader.MinHeaderLength));
        }
    }
}
