using System;
using NUnit.Framework;
using Pixockets;
using System.Net;
using System.Threading;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class ThreadSmartSockTests
    {
        private MockSmartCallbacks _cbs;
        private MockBufferPool _bufferPool;
        private MockSock _bareSock;
        private ThreadSmartSock _threadSmartSock;

        [SetUp]
        public void Setup()
        {
            _cbs = new MockSmartCallbacks();
            _bareSock = new MockSock();
            _bufferPool = new MockBufferPool();
            _threadSmartSock = new ThreadSmartSock(_bufferPool, _bareSock, _cbs);
        }

        [TearDown]
        public void TearDown()
        {
            _threadSmartSock.Close();
        }

        [Test]
        public void ThreadSmartSockRedirects()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23452);

            Assert.AreEqual(PixocketState.NotConnected, _threadSmartSock.State);

            _threadSmartSock.Connect(endPoint.Address, endPoint.Port);  // NotConnected -> Connecting

            Thread.Sleep(50);

            Assert.AreEqual(PixocketState.Connecting, _threadSmartSock.State);
            Assert.AreEqual(_bareSock.RemoteEndPoint, endPoint);

            _threadSmartSock.Close();

            Thread.Sleep(50);

            Assert.AreEqual(1, _bareSock.CloseCalls);
        }

        [Test]
        public void ThreadSmartSockSends()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            _threadSmartSock.Listen(9876);
            Utils.SendConnectRequest(_bareSock, endPoint, _bufferPool);
            WaitSend(1);

            var buffer = Utils.ToBuffer(BitConverter.GetBytes(1234567890), _bufferPool);

            _threadSmartSock.Send(endPoint, buffer.Array, buffer.Offset, buffer.Count, true, true);

            WaitSend(2);

            Assert.AreEqual(2, _bareSock.Sends.Count);  // Connect response + test packet
            Assert.AreEqual(2, _bufferPool.Returned.Count);  // test packet itself + wrapped version
        }

        [Test]
        public void ThreadSmartSockReceives()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 23452);
            _threadSmartSock.Connect(endPoint.Address, endPoint.Port);  // NotConnected -> Connecting
            Utils.SendConnectResponse(_bareSock, endPoint, _bufferPool);
            WaitConnect();

            var buffer = Utils.CreatePacket(1234567890);

            _bareSock.FakeReceive(buffer, 0, buffer.Length, endPoint);

            var receivedPacket = new ReceivedSmartPacket();

            for (int i = 0; i < 1000; i++)
            {
                if (_threadSmartSock.Receive(ref receivedPacket))
                    break;

                Thread.Sleep(1);
            }

            Assert.IsNotNull(receivedPacket.Buffer);
        }

        private void WaitSend(int count)
        {
            for (int i = 0; i < 1000; i++)
            {
                if (_bareSock.Sends.Count < count)
                    Thread.Sleep(1);
                else
                    break;
            }
        }

        private void WaitConnect()
        {
            for (int i = 0; i < 1000; i++)
            {
                if (_threadSmartSock.State == PixocketState.Connected)
                    break;

                Thread.Sleep(1);
            }
        }
    }
}
