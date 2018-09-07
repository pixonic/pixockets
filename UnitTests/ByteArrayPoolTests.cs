using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Pixockets.Core.Buffers;

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
            var byteArrayPool = new DefaultArrayPool<byte>();

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
