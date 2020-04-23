namespace Pixockets.Pools
{
    public abstract class BufferPoolBase
    {
        public abstract byte[] Get(int minLen);

        public abstract void Put(byte[] buf);
    }
}
