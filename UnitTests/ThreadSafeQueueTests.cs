using NUnit.Framework;
using Pixockets;
using System.Threading;

namespace UnitTests
{
    [TestFixture]
    public class ThreadSafeQueueTests
    {
        ThreadSafeQueue<int> _queue;

        [SetUp]
        public void Setup()
        {
            _queue = new ThreadSafeQueue<int>();
        }

        [Test]
        public void QueueSimpleAddTake()
        {
            _queue.Add(1);

            int result = _queue.Take();
            Assert.AreEqual(1, result);
        }

        [Test]
        public void QueueMultiThreadAddTake()
        {
            var fillThread = new Thread(new ThreadStart(FillQueue));
            fillThread.Start();
            for (int i = 0; i < 100; i++)
            {
                int result = _queue.Take();
                Assert.AreEqual(i, result);
            }
            fillThread.Join();
        }

        private void FillQueue()
        {
            for (int i = 0; i < 100; i++)
            {
                _queue.Add(i);
            }
        }
    }
}
