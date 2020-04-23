#if DEBUG
using System;
using System.Collections.Generic;
#endif

namespace Pixockets.Pools
{
    public class ByteBufferPool: BufferPoolBase
    {
        private readonly ArrayPool<byte> _subPool = new ArrayPool<byte>();

#if DEBUG
        private readonly HashSet<byte[]> _unique = new HashSet<byte[]>();
#endif

        public override byte[] Get(int minLen)
        {
            var buffer = _subPool.Rent(minLen);

#if DEBUG
            lock (_unique)
            {
                _unique.Remove(buffer);
            }
#endif

            return buffer;
        }

        public override void Put(byte[] buffer)
        {
#if DEBUG
            lock (_unique)
            {
                if (_unique.Contains(buffer))
                {
                    throw new ArgumentException("Double return of size " + buffer.Length);
                }

                _unique.Add(buffer);
            }
#endif
            _subPool.Return(buffer);
        }
    }
}
