using NUnit.Framework;
using Pixockets;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using Pixockets.Debug;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class MTUTests
    {
        [Test]
        public void SendMoreThanMTUDroppedOnReceiveByBareSock()
        {
            var bufferPool = new CoreBufferPool();
            var sock = new BareSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub());
            sock.Listen(23459);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23459);
            udpClient.Send(new byte[BareSock.MTU + 1], BareSock.MTU + 1);

            var receivedPacket = Utils.WaitOnReceive(sock);

            Assert.IsNull(receivedPacket.Buffer);
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(0, receivedPacket.Length);

            sock.Close();
        }

        [Test]
        public void SendMoreThanMTUDroppedOnReceiveByThreadSock()
        {
            var bufferPool = new CoreBufferPool();
            var sock = new ThreadSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub());
            sock.Listen(23460);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23460);
            udpClient.Send(new byte[BareSock.MTU + 1], BareSock.MTU + 1);

            var receivedPacket = Utils.WaitOnReceive(sock);

            Assert.IsNull(receivedPacket.Buffer);
            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(0, receivedPacket.Length);

            sock.Close();
        }

    }
}
