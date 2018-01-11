
namespace Pixockets
{
    public class NotAckedPacket : IPoolable
    {
        public int Offset;
        public int Length;
        public byte[] Buffer;
        public int SendTicks;
        public ushort SeqNum;

        public void Strip()
        {
            Buffer = null;
        }
    }
}
