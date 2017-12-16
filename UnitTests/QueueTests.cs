using NUnit.Framework;
using Pixockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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

            MockCallbacks cbs = new MockCallbacks();
            BareSock sock = new BareSock(cbs);

            sock.Connect(23450);
            sock.SendTo(BitConverter.GetBytes(1), 0, 4);
            sock.SendTo(BitConverter.GetBytes(2), 0, 4);
            sock.SendTo(BitConverter.GetBytes(3), 0, 4);

            receiveTask.Wait(1000);
            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.LessOrEqual(1, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));

            receiveTask = udpClient.ReceiveAsync();
            receiveTask.Wait(1000);
            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.LessOrEqual(2, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));

            receiveTask = udpClient.ReceiveAsync();
            receiveTask.Wait(1000);
            Assert.AreEqual(TaskStatus.RanToCompletion, receiveTask.Status);
            Assert.AreEqual(3, BitConverter.ToInt32(receiveTask.Result.Buffer, 0));
        }
    }
}
