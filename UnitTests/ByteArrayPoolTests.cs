using System.Collections.Generic;
using NUnit.Framework;
using Pixockets.Pools;

namespace UnitTests
{
    [TestFixture]
    public class ByteArrayPoolTests
    {
        private const int BuffersToRent = 256;

        [TestCase(5)]
        [TestCase(25)]
        [TestCase(125)]
        [TestCase(625)]
        [TestCase(1625)]
        [TestCase(3125)]
        [TestCase(15625)]
        public void RentAndReturnAreExceptionFree(int arraySize)
        {
            var byteArrayPool = new ArrayPool<byte>();

            var capacitor = new List<byte[]>();
            // Rent
            for (int i = 0; i < BuffersToRent; ++i)
            {
                var buffer = byteArrayPool.Rent(arraySize);
                capacitor.Add(buffer);
            }
            // Return
            for (int i = 0; i < BuffersToRent; ++i)
            {
                var buffer = capacitor[i];
                byteArrayPool.Return(buffer);
            }
            capacitor.Clear();
            // Rent again
            for (int i = 0; i < BuffersToRent; ++i)
            {
                var buffer = byteArrayPool.Rent(arraySize);
                capacitor.Add(buffer);
            }
            // Return again
            for (int i = 0; i < BuffersToRent; ++i)
            {
                var buffer = capacitor[i];
                byteArrayPool.Return(buffer);
            }
        }
    }
}
