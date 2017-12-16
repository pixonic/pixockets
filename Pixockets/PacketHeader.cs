using System;

namespace Pixockets
{
    public struct PacketHeader
    {
        public ushort Length;

        public PacketHeader(ushort length)
        {
            Length = length;
        }

        public PacketHeader(byte[] buffer, int offset)
        {
            Length = BitConverter.ToUInt16(buffer, offset);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            int pos = offset;
            if (BitConverter.IsLittleEndian)
            {
                buffer[pos++] = (byte)(Length);
                buffer[pos] = (byte)(Length >> 8);
            }
            else
            {
                buffer[pos++] = (byte)(Length >> 8);
                buffer[pos] = (byte)(Length);
            }
        }
    }
}
