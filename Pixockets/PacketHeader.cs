using System;
using System.Collections.Generic;
using System.IO;
using Pixockets.Pools;

namespace Pixockets
{
    public class PacketHeader : IPoolable
    {
        public const int MinHeaderLength = 5;
        public const int EmptySessionId = 0;

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
                    res += 1 + 2 * Acks.Count;
                }
                if ((Flags & ContainsFrag) != 0)
                {
                    res += 6;
                }

                return res;
            }
        }

        public const byte ContainsSeq = 0x1;
        public const byte ContainsAck = 0x2;
        public const byte ContainsFrag = 0x4;
        public const byte Connect = 0x8;
        public const byte NeedsAck = 0x10;
        public const byte ShouldBeZero = 0xFF ^ (ContainsSeq | ContainsAck | ContainsFrag | Connect | NeedsAck);

        public byte Flags;
        // We need this to detect truncated datagrams
        public ushort Length;
        public ushort SeqNum;
        public readonly List<ushort> Acks;  // Acked SeqNums
        public ushort FragId;  // Id of this fragment
        public ushort FragNum;  // Number of this fragment
        public ushort FragCount;  // Count of fragments in this sequence
        public ushort SessionId;

        public PacketHeader()
        {
            Flags = 0;
            SessionId = 0;
            Acks = new List<ushort>();
        }

        public void Init(byte[] buffer, int offset)
        {
            try
            {
                Length = BitConverter.ToUInt16(buffer, offset);
                Flags = buffer[offset + 2];
                int pos = offset + 3;
                SessionId = BitConverter.ToUInt16(buffer, pos);
                pos += 2;
                if ((Flags & ContainsSeq) != 0)
                {
                    SeqNum = BitConverter.ToUInt16(buffer, pos);
                    pos += 2;
                }
                if ((Flags & ContainsAck) != 0)
                {
                    int acksCount = buffer[pos++];
                    for (int i = 0; i < acksCount; ++i)
                    {
                        ushort ack = BitConverter.ToUInt16(buffer, pos);
                        pos += 2;
                        Acks.Add(ack);
                    }
                }
                if ((Flags & ContainsFrag) != 0)
                {
                    FragId = BitConverter.ToUInt16(buffer, pos);
                    FragNum = BitConverter.ToUInt16(buffer, pos + 2);
                    FragCount = BitConverter.ToUInt16(buffer, pos + 4);
                    //pos += 6;
                }

                if ((Flags & ShouldBeZero) != 0)
                    throw new FormatException("Wrong Header Format");
            }
            catch (Exception)
            {
                // Bad format,
                // Zero all
                Length = 0;
                Flags = 0;
                SeqNum = 0;
                FragId = 0;
                FragNum = 0;
                FragCount = 0;
                SessionId = 0;
            }
        }

        public void SetSeqNum(ushort seqNum)
        {
            Flags |= ContainsSeq;
            SeqNum = seqNum;
        }

        public void AddAck(ushort seqNum)
        {
            Flags |= ContainsAck;
            Acks.Add(seqNum);
        }

        public void SetNeedAck()
        {
            Flags |= NeedsAck;
        }

        public void SetConnect()
        {
            Flags |= Connect;
        }

        public void SetSessionId(ushort sessionId)
        {
            SessionId = sessionId;
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
            pos = WriteUInt16(SessionId, buffer, pos);
            if ((Flags & ContainsSeq) != 0)
            {
                pos = WriteUInt16(SeqNum, buffer, pos);
            }
            if ((Flags & ContainsAck) != 0)
            {
                var acksCount = (byte)Acks.Count;
                buffer[pos++] = acksCount;
                for (int i = 0; i < acksCount; ++i)
                {
                    pos = WriteUInt16(Acks[i], buffer, pos);
                }
            }
            if ((Flags & ContainsFrag) != 0)
            {
                pos = WriteUInt16(FragId, buffer, pos);
                pos = WriteUInt16(FragNum, buffer, pos);
                /*pos = */WriteUInt16(FragCount, buffer, pos);
            }
        }

        private static int WriteUInt16(ushort value, byte[] buffer, int pos)
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
            SessionId = EmptySessionId;
            Acks.Clear();
        }
    }
}
