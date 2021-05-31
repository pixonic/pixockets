namespace Pixockets
{
    public struct NotAckedPacket
    {
        public int Offset;
        public int Length;
        public byte[] Buffer;
        public int SendTicks;
        public ushort SeqNum;
    }
}
