using System;
using System.Collections.Generic;
using System.Net;

namespace Pixockets
{
    public class SequenceState
    {
        public ushort SeqNum;
        public readonly List<NotAckedPacket> NotAcked = new List<NotAckedPacket>();
        public int LastActive;
        public byte FragId;
        public object SyncObj = new object();

        private List<FragmentedPacket> _frags = new List<FragmentedPacket>();

        public SequenceState()
        {
            LastActive = Environment.TickCount;
        }

        public ushort NextSeqNum()
        {
            ushort seqNum;
            lock (SyncObj)
            {
                seqNum = SeqNum++;
            }

            return seqNum;
        }

        public void AddFragment(byte[] buffer, int offset, int length, PacketHeader header)
        {
            var fragBuffer = new FragmentBuffer
            {
                Buffer = buffer,
                Offset = offset,
                Length = length,
                Header = header,
            };

            lock (SyncObj)
            {
                var frag = GetFragmentedPacket(header);

                // add fragment buffer to it
                frag.Buffers.Add(fragBuffer);

                // sort fragments
                var fragIdx = frag.Buffers.Count - 1;
                while (fragIdx > 0 &&
                    frag.Buffers[fragIdx].Header.FragNum < frag.Buffers[fragIdx - 1].Header.FragNum)
                {
                    Exch(frag, fragIdx - 1, fragIdx);
                    fragIdx--;
                }

                // remove if duplicate
                if (fragIdx > 0 && frag.Buffers[fragIdx].Header.FragNum < frag.Buffers[fragIdx - 1].Header.FragNum)
                {
                    frag.Buffers.RemoveAt(fragIdx);
                }
            }
        }

        public void CombineIfFull(PacketHeader header, IPEndPoint endPoint, ReceiverBase cbs)
        {
            byte[] combinedBuffer;
            int fullLength = 0;

            lock (SyncObj)
            {
                // TODO: validate that headers of all fragments match
                var frag = GetFragmentedPacket(header);

                // TODO: pool packet headers
                if (frag.Buffers.Count < header.FragCount)
                {
                    return;
                }

                var buffersCount = frag.Buffers[0].Header.FragCount;
                for (int i = 0; i < buffersCount; ++i)
                {
                    fullLength += frag.Buffers[i].Length;
                }

                // TODO: pool buffers
                combinedBuffer = new byte[fullLength];
                var targetOffset = 0;
                for (int i = 0; i < buffersCount; ++i)
                {
                    var srcBuffer = frag.Buffers[i];
                    Array.Copy(srcBuffer.Buffer, srcBuffer.Offset, combinedBuffer, targetOffset, srcBuffer.Length);
                    targetOffset += frag.Buffers[i].Length;
                }
            }

            cbs.OnReceive(combinedBuffer, 0, fullLength, endPoint);
        }

        private void Exch(FragmentedPacket frag, int i, int j)
        {
            var temp = frag.Buffers[i];
            frag.Buffers[i] = frag.Buffers[j];
            frag.Buffers[j] = temp;
        }

        // find fragmented packet
        private FragmentedPacket GetFragmentedPacket(PacketHeader header)
        {
            var packetsCount = _frags.Count;
            var found = false;
            int i;
            for (i = 0; i < packetsCount; ++i)
            {
                if (_frags[i].FragId == header.FragId)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                return _frags[i];
            }
            // create new one
            else
            {
                // TODO: pool them
                return new FragmentedPacket();
            }
        }
    }
}
