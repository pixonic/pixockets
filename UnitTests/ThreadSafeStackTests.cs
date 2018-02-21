using NUnit.Framework;
using Pixockets;
using System.Threading;

namespace UnitTests
{
    [TestFixture]
    public class ThreadSafeStackTests
    {
        ThreadSafeStack<int> _stack;

        [SetUp]
        public void Setup()
        {
            _stack = new ThreadSafeStack<int>();
        }

        [Test]
        public void StackSimplePushPop()
        {
            int result;
            Assert.IsFalse(_stack.TryPop(out result));

            _stack.Push(1);

            Assert.IsTrue(_stack.TryPop(out result));
            Assert.AreEqual(1, result);
            Assert.IsFalse(_stack.TryPop(out result));
        }

        [Test]
        public void StackMultiThreadPushPop()
        {
            var fillThread = new Thread(new ThreadStart(FillStack));
            fillThread.Start();
            for (int i = 0; i < 100; i++)
            {
                int result = -1;
                for (int j = 0; j < 100; j++)
                {
                    if (_stack.TryPop(out result))
                    {
                        break;
                    }
                    Thread.Sleep(1);
                }
                Assert.AreEqual(3, result);
                result = -1;
            }
            fillThread.Join();
        }

        private void FillStack()
        {
            for (int i = 0; i < 100; i++)
            {
                _stack.Push(3);
            }
        }
    }
}
