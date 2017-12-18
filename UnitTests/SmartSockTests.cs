using NUnit.Framework;
using Pixockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
            var sock = new SmartSock(cbs);
            sock.Connect(IPAddress.Loopback, 23451);
            sock.ReceiveFrom();

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)6), 0, 2);
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);
            var buffer = ms.ToArray();
            udpClient.Send(buffer, buffer.Length, (IPEndPoint)sock.SubSock.SysSock.LocalEndPoint);

            Utils.WaitOnReceive(cbs);

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(123456789, BitConverter.ToInt32(cbs.OnReceiveCalls[0].Buffer, PacketHeader.HeaderLength));
            Assert.AreEqual(PacketHeader.HeaderLength, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(4, cbs.OnReceiveCalls[0].Length);
        }
    }
}
