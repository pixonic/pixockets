using NUnit.Framework;
using Pixockets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            _sock = new SmartSock(ArrayPool<byte>.Shared, _bareSock, _cbs);
        }

        [Test]
        public void DisconnectedOnTimeout()
        {
            _sock.ConnectionTimeout = 1;
            _sock.Connect(IPAddress.Loopback, 23451);
            _sock.Receive();

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)7), 0, 2);  // Length
            ms.WriteByte(0);  // Flags
            ms.Write(BitConverter.GetBytes(123456789), 0, 4);  // Payload
            var buffer = ms.ToArray();

            // Simulate send from UdpClient
            _bareSock.Callbacks.OnReceive(buffer, 0, buffer.Length, new IPEndPoint(IPAddress.Loopback, 54321));

            Thread.Sleep(20);
            _sock.Tick();

            Assert.AreEqual(1, _cbs.OnConnectCalls.Count);
            Assert.AreEqual(1, _cbs.OnDisconnectCalls.Count);
        }
    }
}
