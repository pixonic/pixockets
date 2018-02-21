using Core.Buffers;

namespace Pixockets
{
    public class CoreBufferPool : BufferPoolBase
    {
        private readonly ArrayPool<byte> _arrayPool = new DefaultArrayPool<byte>();

        public override byte[] Get(int minLen)
        {
            return _arrayPool.Rent(minLen);
        }

        public override void Put(byte[] buf)
        {
            _arrayPool.Return(buf);
        }
    }
}
