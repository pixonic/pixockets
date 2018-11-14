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
        public void QueueSimpleAddTryTake()
        {
            _queue.Add(7);

            int item;
            Assert.IsTrue(_queue.TryTake(out item));
            Assert.AreEqual(7, item);
        }

        [Test]
        public void QueueMultipleAddTake()
        {
            FillQueue();
            for (int i = 0; i < 100; i++)
            {
                int result = _queue.Take();
                Assert.AreEqual(i, result);
            }
        }

        [Test]
        public void QueueInterleavedAddTake()
        {
            AddSome(0, 20);
            TakeSome(0, 15);

            AddSome(21, 40);
            AddSome(41, 70);

            TakeSome(16, 50);
            TakeSome(51, 70);
        }

        [Test]
        public void QueueMultipleAddTryTake()
        {
            FillQueue();
            for (int i = 0; i < 100; i++)
            {
                int item;
                Assert.IsTrue(_queue.TryTake(out item));
                Assert.AreEqual(i, item);
            }
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

        private void AddSome(int from, int to)
        {
            for (int i = from; i <= to; i++)
            {
                _queue.Add(i);
            }
        }

        private void TakeSome(int from, int to)
        {
            for (int i = from; i <= to; i++)
            {
                int item;
                Assert.IsTrue(_queue.TryTake(out item));
                Assert.AreEqual(i, item);
            }
        }

        [Test]
        public void AddMoreThanLimit()
        {
            for (int i = 0; i < ThreadSafeQueue<int>.SizeLimit + 100; i++)
            {
                _queue.Add(i);
            }

            Assert.AreEqual(100, _queue.Take());
        }

    }
}
