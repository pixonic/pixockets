using System;
using System.IO;

namespace Pixockets
{
    public class PacketHeader : IPoolable
    {
        public const int MinHeaderLength = 3;

        public int HeaderLength {
            get
            {
                int res = MinHeaderLength;
                if ((Flags & ContainsSeq) != 0)
                {
                    res += 2;
                }
                if ((Flags & ContainsAck) != 0)
                {
                    res += 2;
                }
                if ((Flags & ContainsFrag) != 0)
                {
                    res += 6;
                }

                return res;
            }
        }

        private const byte ContainsSeq = 0x1;
        public const byte ContainsAck = 0x2;
        public const byte ContainsFrag = 0x4;
        private const byte Reserved1 = 0x8;
        public const byte NeedsAck = 0x10;

        public byte Flags;
        // We need this to detect truncated datagrams
        public ushort Length;
        public ushort SeqNum;
        public ushort Ack;  // Acked SeqNum
        public ushort FragId;  // Id of this fragment
        public ushort FragNum;  // Number of this fragment
        public ushort FragCount;  // Count of fragments in this sequence

        public PacketHeader()
        {
            Flags = 0;
        }

        public void Init(byte[] buffer, int offset)
        {
            Length = BitConverter.ToUInt16(buffer, offset);
            Flags = buffer[offset + 2];
            int pos = offset + 3;
            if ((Flags & ContainsSeq) != 0)
            {
                SeqNum = BitConverter.ToUInt16(buffer, pos);
                pos += 2;
            }
            if ((Flags & ContainsAck) != 0)
            {
                Ack = BitConverter.ToUInt16(buffer, pos);
                pos += 2;
            }
            if ((Flags & ContainsFrag) != 0)
            {
                FragId = BitConverter.ToUInt16(buffer, pos);
                FragNum = BitConverter.ToUInt16(buffer, pos + 2);
                FragCount = BitConverter.ToUInt16(buffer, pos + 4);
                pos += 6;
            }
        }

        public PacketHeader(ushort length)
        {
            Length = length;
            Flags = 0;
        }

        public void SetSeqNum(ushort seqNum)
        {
            Flags |= ContainsSeq;
            SeqNum = seqNum;
        }

        public void SetAck(ushort seqNum)
        {
            Flags |= ContainsAck;
            Ack = seqNum;
        }


        public void SetNeedAck()
        {
            Flags |= NeedsAck;
        }

        public bool GetNeedAck()
        {
            return (Flags & NeedsAck) != 0;
        }

        public void SetFrag(ushort fragId, ushort fragNum, ushort fragCount)
        {
            FragId = fragId;
            FragNum = fragNum;
            FragCount = fragCount;
            Flags |= ContainsFrag;
        }

        // For unit-testing only
        public void WriteTo(Stream stream)
        {
            var buffer = new byte[64];
            WriteTo(buffer, 0);
            stream.Write(buffer, 0, HeaderLength);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            int pos = offset;
            pos = WriteUInt16(Length, buffer, pos);
            buffer[pos++] = Flags;
            if ((Flags & ContainsSeq) != 0)
            {
                pos = WriteUInt16(SeqNum, buffer, pos);
            }
            if ((Flags & ContainsAck) != 0)
            {
                pos = WriteUInt16(Ack, buffer, pos);
            }
            if ((Flags & ContainsFrag) != 0)
            {
                pos = WriteUInt16(FragId, buffer, pos);
                pos = WriteUInt16(FragNum, buffer, pos);
                pos = WriteUInt16(FragCount, buffer, pos);
            }
        }

        private int WriteUInt16(ushort value, byte[] buffer, int pos)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[pos++] = (byte)(value);
                buffer[pos++] = (byte)(value >> 8);
            }
            else
            {
                buffer[pos++] = (byte)(value >> 8);
                buffer[pos++] = (byte)(value);
            }

            return pos;
        }

        public void Strip()
        {
            Flags = 0;
        }
    }
}
