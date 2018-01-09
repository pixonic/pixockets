﻿using System;
using System.Collections.Generic;
using System.Net;

namespace Pixockets
{
    public class SequenceState
    {
        public int LastActive;

        private bool _connected = false;
        private ushort _nextSeqNum;
        private ushort _nextFragId;
        private readonly List<NotAckedPacket> _notAcked = new List<NotAckedPacket>();
        private object _syncObj = new object();
        private List<FragmentedPacket> _frags = new List<FragmentedPacket>();
        private Pool<NotAckedPacket> _notAckedPool;

        public SequenceState(Pool<NotAckedPacket> notAckedPool)
        {
            _notAckedPool = notAckedPool;
            LastActive = Environment.TickCount;
        }

        public bool CheckConnected()
        {
            lock (_syncObj)
            {
                if (!_connected)
                {
                    _connected = true;
                    return true;
                }

                return false;
            }
        }

        public ushort NextSeqNum()
        {
            lock (_syncObj)
            {
                return _nextSeqNum++;
            }
        }

        public ushort tNextFragId()
        {
            lock (_syncObj)
            {
                return _nextFragId++;
            }
        }

        public void AddFragment(byte[] buffer, int offset, int length, PacketHeader header)
        {
            var fragBuffer = new FragmentBuffer
            {
                Buffer = buffer,
                Offset = offset + header.HeaderLength,  // payload offset
                Length = length - header.HeaderLength,  // payload length
                Header = header,
            };

            lock (_syncObj)
            {
                var frag = GetFragmentedPacket(header);
                frag.LastActive = Environment.TickCount;

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
                if (fragIdx > 0 && frag.Buffers[fragIdx].Header.FragNum == frag.Buffers[fragIdx - 1].Header.FragNum)
                {
                    frag.Buffers.RemoveAt(fragIdx);
                }
            }
        }

        public void CombineIfFull(PacketHeader header, IPEndPoint endPoint, SmartReceiverBase cbs)
        {
            byte[] combinedBuffer;
            int fullLength = 0;

            lock (_syncObj)
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

        public void Tick(IPEndPoint endPoint, SockBase sock, int now, int ackTimeout, int fragmentTimeout)
        {
            lock (_syncObj)
            {
                var notAckedCount = _notAcked.Count;
                for (int i = 0; i < notAckedCount; ++i)
                {
                    var packet = _notAcked[i];
                    if (now - packet.SendTicks > ackTimeout)
                    {
                        sock.Send(endPoint, packet.Buffer, packet.Offset, packet.Length);
                        packet.SendTicks = now;
                    }
                }

                var packetsCount = _frags.Count;
                for (int i = packetsCount - 1; i >= 0; --i)
                {
                    var frag = _frags[i];
                    if (SmartSock.TimeDelta(frag.LastActive, now) > fragmentTimeout)
                    {
                        _frags.RemoveAt(i);
                    }
                }
            }
        }

        public void AddNotAcked(NotAckedPacket packet)
        {
            lock (_syncObj)
            {
                _notAcked.Add(packet);
            }
        }

        public void ReceiveAck(IPEndPoint endPoint, ushort ack)
        {
            lock (_syncObj)
            {
                var notAckedCount = _notAcked.Count;
                for (int i = 0; i < notAckedCount; ++i)
                {
                    var packet = _notAcked[i];
                    if (packet.SeqNum == ack)
                    {
                        _notAcked.RemoveAt(i);
                        _notAckedPool.Put(packet);
                        break;
                    }
                }
            }
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
                var frag = new FragmentedPacket();
                frag.FragId = header.FragId;
                _frags.Add(frag);
                return frag;
            }
        }
    }
}
