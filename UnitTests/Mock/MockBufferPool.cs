using System.Collections.Generic;
using Pixockets.Pools;

namespace UnitTests.Mock
{
    public class MockBufferPool : BufferPoolBase
    {
        public HashSet<byte[]> Rented = new HashSet<byte[]>();
        public HashSet<byte[]> Returned = new HashSet<byte[]>();
        public int Alien;

        public override byte[] Get(int minLen)
        {
            var buf = new byte[minLen];
            Rented.Add(buf);
            return buf;
        }

        public override void Put(byte[] buf)
        {
            if (!Rented.Contains(buf))
            {
                Alien++;
            }
            Returned.Add(buf);
        }
    }
}
