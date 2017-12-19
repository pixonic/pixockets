﻿using System;

namespace Pixockets
{
    public class PacketHeader
    {
        public const int MinHeaderLength = 3;

        public int HeaderLength {
            get
            {
                int res = MinHeaderLength;
                if ((Flags & ContainSeq) != 0)
                {
                    res += 2;
                }
                if ((Flags & ContainAcks) != 0)
                {
                    res += 4;
                }
                if ((Flags & ContainFrag) != 0)
                {
                    res += 5;
                }

                return res;
            }
        }

        private const byte ContainSeq = 0x1;
        private const byte ContainAcks = 0x2;
        private const byte ContainFrag = 0x4;
        private const byte Reserved1 = 0x8;
        public const byte NeedAck = 0x10;

        public byte Flags;
        // We need this to detect truncated datagrams
        public ushort Length;
        public ushort SeqNum;
        public ushort Ack;  // First acked SeqNum
        public ushort Acks;  // bits of other Acks
        public byte FragId;  // Id of this fragment
        public ushort FragNum;  // Number of this fragment
        public ushort FragCount;  // Count of fragments in this sequence

        public PacketHeader()
        {
            Flags = 0;
        }

        public PacketHeader(ushort length)
        {
            Length = length;
            Flags = 0;
        }

        public void SetSeqNum(ushort seqNum)
        {
            Flags |= ContainSeq;
            SeqNum = seqNum;
        }

        public PacketHeader(byte[] buffer, int offset)
        {
            Length = BitConverter.ToUInt16(buffer, offset);
            Flags = buffer[offset+2];
            int pos = offset + 3;
            if ((Flags & ContainSeq) != 0)
            {
                SeqNum = BitConverter.ToUInt16(buffer, pos);
                pos += 2;
            }
            if ((Flags & ContainAcks) != 0)
            {
                Ack = BitConverter.ToUInt16(buffer, pos);
                Acks = BitConverter.ToUInt16(buffer, pos+2);
                pos += 4;
            }
            if ((Flags & ContainFrag) != 0)
            {
                FragId = buffer[pos++];
                FragNum = BitConverter.ToUInt16(buffer, pos+2);
                FragCount = BitConverter.ToUInt16(buffer, pos+4);
                pos += 5;
            }
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            int pos = offset;
            pos = WriteUInt16(Length, buffer, pos);
            buffer[pos++] = Flags;
            if ((Flags & ContainSeq) != 0)
            {
                pos = WriteUInt16(SeqNum, buffer, pos);
            }
            if ((Flags & ContainAcks) != 0)
            {
                pos = WriteUInt16(Ack, buffer, pos);
                pos = WriteUInt16(Acks, buffer, pos);
            }
            if ((Flags & ContainFrag) != 0)
            {
                buffer[pos++] = FragId;
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
    }
}
