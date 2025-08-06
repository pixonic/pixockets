using System.Collections.Generic;
using Pixockets.Pools;

namespace UnitTests.Mock
{
    public class MockBufferPool : BufferPoolBase
    {
        public readonly HashSet<byte[]> Rented = new HashSet<byte[]>();
        public readonly HashSet<byte[]> Returned = new HashSet<byte[]>();
        public int Alien;
        private readonly object _syncObject = new object();

        public override byte[] Get(int minLen)
        {
            lock (_syncObject)
            {
                var buf = new byte[minLen];
                Rented.Add(buf);
                return buf;
            }
        }

        public override void Put(byte[] buf)
        {
            lock (_syncObject)
            {
                if (!Rented.Contains(buf))
                    Alien++;

                Returned.Add(buf);
            }
        }
    }
}
