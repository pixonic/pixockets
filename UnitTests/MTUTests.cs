using NUnit.Framework;
using Pixockets;
using System.Net;
using System.Net.Sockets;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class MTUTests
    {
        [Test]
        public void SendMoreThanMTUClamped()
        {
            var bufferPool = new CoreBufferPool();
            BareSock sock = new BareSock(bufferPool, AddressFamily.InterNetwork);
            sock.Listen(23459);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23459);
            udpClient.Send(new byte[BareSock.MTU + 1], BareSock.MTU + 1);

            var receivedPacket = Utils.WaitOnReceive(sock);

            Assert.AreEqual(0, receivedPacket.Offset);
            Assert.AreEqual(BareSock.MTU, receivedPacket.Length);
        }
    }
}
