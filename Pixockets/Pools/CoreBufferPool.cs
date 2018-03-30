using Core.Buffers;

#if DEBUG
using System;
using System.Collections.Generic;
#endif

namespace Pixockets
{
    public class CoreBufferPool: BufferPoolBase
    {
        private readonly ArrayPool<byte> _arrayPool;

#if DEBUG
        private readonly HashSet<byte[]> _unique = new HashSet<byte[]>();
#endif

        public CoreBufferPool()
            : this(new DefaultArrayPool<byte>())
        {
        }

        public CoreBufferPool(ArrayPool<byte> arrayPool)
        {
            _arrayPool = arrayPool;
        }

        public override byte[] Get(int minLen)
        {
            var buffer = _arrayPool.Rent(minLen);

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
            _arrayPool.Return(buffer);
        }
    }
}
