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
            MockCallbacks cbs = new MockCallbacks();
            var bufferPool = new CoreBufferPool();
            BareSock sock = new BareSock(bufferPool);
            sock.SetCallbacks(cbs);
            sock.Receive(23459);

            UdpClient udpClient = new UdpClient();
            udpClient.Connect(IPAddress.Loopback, 23459);
            udpClient.Send(new byte[BareSock.MTU + 1], BareSock.MTU + 1);

            Utils.WaitOnReceive(cbs);

            Assert.AreEqual(1, cbs.OnReceiveCalls.Count);
            Assert.AreEqual(0, cbs.OnReceiveCalls[0].Offset);
            Assert.AreEqual(BareSock.MTU, cbs.OnReceiveCalls[0].Length);
        }
    }
}
