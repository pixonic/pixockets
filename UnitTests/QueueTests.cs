using NUnit.Framework;
using Pixockets;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Pixockets.DebugTools;
using Pixockets.Pools;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class QueueTests
    {
        [Test]
        public void MultiplePacketsSent()
        {
            UdpClient udpClient = new UdpClient(23450);
            var receiveTask = udpClient.ReceiveAsync();

            var bufferPool = new ByteBufferPool();
            var sock = new BareSock(bufferPool, AddressFamily.InterNetwork, new LoggerStub());

            sock.Connect(IPAddress.Loopback, 23450);
            sock.Send(BitConverter.GetBytes(1), 0, 4, false);
            sock.Send(BitConverter.GetBytes(2), 0, 4, false);
            sock.Send(BitConverter.GetBytes(3), 0, 4, false);

            receiveTask.Wait(2000);
            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.LessOrEqual(1, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));

            receiveTask = udpClient.ReceiveAsync();
            receiveTask.Wait(2000);
            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.LessOrEqual(2, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));

            // This test is flaky...
            receiveTask = udpClient.ReceiveAsync();
            receiveTask.Wait(2000);
            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(3, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));

            sock.Close();
        }
    }
}
