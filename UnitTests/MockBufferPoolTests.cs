using NUnit.Framework;
using UnitTests.Mock;

namespace UnitTests
{
    [TestFixture]
    public class MockBufferPoolTests
    {
        [Test]
        public void RentedBufferReturned()
        {
            var pool = new MockBufferPool();
            var buffer = pool.Get(128);

            pool.Put(buffer);

            Assert.AreEqual(0, pool.Alien);
            Assert.AreEqual(1, pool.Rented.Count);
            Assert.AreEqual(1, pool.Returned.Count);
        }

        [Test]
        public void AlienBufferDetected()
        {
            var pool = new MockBufferPool();
            var bufferFromPool1 = pool.Get(128);
            var bufferFromPool2 = pool.Get(256);

            pool.Put(new byte[256]);

            Assert.AreEqual(1, pool.Alien);
            Assert.AreEqual(2, pool.Rented.Count);
            Assert.AreEqual(1, pool.Returned.Count);
        }

        [Test]
        public void ModifiedBufferIsNotAnAlien()
        {
            var pool = new MockBufferPool();
            var buffer = pool.Get(256);
            for (byte i = 0; i < 100; i++)
                buffer[i] = i;

            pool.Put(buffer);

            Assert.AreEqual(0, pool.Alien);
            Assert.AreEqual(1, pool.Rented.Count);
            Assert.AreEqual(1, pool.Returned.Count);
        }
    }
}
