﻿using System;
using System.Collections.Generic;
using System.Net;

namespace Pixockets
{
    public class SequenceState : IPoolable
    {
        public int LastActive;
        private const int ReceivedSeqNumBufferSize = 32;

        private bool _connected = false;
        private ushort _nextSeqNum;
        private ushort _nextFragId;
        private readonly List<NotAckedPacket> _notAcked = new List<NotAckedPacket>();
        private object _syncObj = new object();
        private List<FragmentedPacket> _frags = new List<FragmentedPacket>();
        private int _lastReceivedSeqNum = -1;  // int for calculations
        private ushort[] _lastRecevedSeqNums = new ushort[ReceivedSeqNumBufferSize];
        private int _lastRecevedSeqNumIdx = 0;

        private Pool<NotAckedPacket> _notAckedPool;
        private Pool<FragmentedPacket> _fragPacketsPool;
        private BufferPoolBase _buffersPool;

        public SequenceState()
        {
        }

        public void Init(BufferPoolBase buffersPool, Pool<FragmentedPacket> fragPacketsPool, Pool<NotAckedPacket> notAckedPool)
        {
            _buffersPool = buffersPool;
            _fragPacketsPool = fragPacketsPool;
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

        public ushort NextFragId()
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
                FragNum = header.FragNum,
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
                    frag.Buffers[fragIdx].FragNum < frag.Buffers[fragIdx - 1].FragNum)
                {
                    Exch(frag, fragIdx - 1, fragIdx);
                    fragIdx--;
                }

                // remove if duplicate
                if (fragIdx > 0 && frag.Buffers[fragIdx].FragNum == frag.Buffers[fragIdx - 1].FragNum)
                {
                    frag.Buffers.RemoveAt(fragIdx);
                }
            }
        }

        public ReceivedSmartPacket CombineIfFull(PacketHeader header, IPEndPoint endPoint, SmartReceiverBase cbs)
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
                    return null;
                }

                var buffersCount = frag.FragCount;
                for (int i = 0; i < buffersCount; ++i)
                {
                    fullLength += frag.Buffers[i].Length;
                }

                combinedBuffer = _buffersPool.Get(fullLength);
                var targetOffset = 0;
                for (int i = 0; i < buffersCount; ++i)
                {
                    var srcBuffer = frag.Buffers[i];
                    Array.Copy(srcBuffer.Buffer, srcBuffer.Offset, combinedBuffer, targetOffset, srcBuffer.Length);
                    targetOffset += frag.Buffers[i].Length;
                    _buffersPool.Put(srcBuffer.Buffer);
                }

                // TODO: optimize?
                _frags.Remove(frag);
                _fragPacketsPool.Put(frag);
            }

            // TODO: calculate it?
            bool inOrder = true;

            var packet = new ReceivedSmartPacket();
            packet.Buffer = combinedBuffer;
            packet.Offset = 0;
            packet.Length = fullLength;
            packet.EndPoint = endPoint;
            packet.InOrder = inOrder;
            return packet;
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
                        sock.Send(endPoint, packet.Buffer, packet.Offset, packet.Length, false);
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
                        _fragPacketsPool.Put(frag);
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
                        _buffersPool.Put(packet.Buffer);
                        _notAckedPool.Put(packet);
                        break;
                    }
                }
            }
        }

        public void Strip()
        {
            lock (_syncObj)
            {
                var notAckedCount = _notAcked.Count;
                for (int i = 0; i < notAckedCount; ++i)
                {
                    var packet = _notAcked[i];
                    _buffersPool.Put(packet.Buffer);
                    _notAckedPool.Put(packet);
                }
                _notAcked.Clear();

                _connected = false;
                _nextSeqNum = 0;
                _nextFragId = 0;
                var fragCount = _frags.Count;
                for (int i = 0; i < fragCount; ++i)
                {
                    _fragPacketsPool.Put(_frags[i]);
                }
                _frags.Clear();

                _lastReceivedSeqNum = -1;
                Array.Clear(_lastRecevedSeqNums, 0, _lastRecevedSeqNums.Length);
            }
        }

        public void RegisterIncoming(ushort seqNum)
        {
            lock (_syncObj)
            {
                _lastReceivedSeqNum = seqNum;
                _lastRecevedSeqNums[_lastRecevedSeqNumIdx++] = seqNum;
                if (_lastRecevedSeqNumIdx == ReceivedSeqNumBufferSize)
                {
                    _lastRecevedSeqNumIdx = 0;
                }
            }
        }

        // TODO: drop dublicates
        public bool IsInOrder(ushort seqNum)
        {
            //TODO: fix corner cases
            int delta = seqNum - _lastReceivedSeqNum;
            if (delta < -32000)
            {
                delta += 65536;
            }
            else if (delta > 32000)
            {
                delta = -1;
            }
            return delta > 0;
        }

        public bool IsDuplicate(ushort seqNum)
        {
            for (int i = 0; i < ReceivedSeqNumBufferSize; ++i)
            {
                if (_lastRecevedSeqNums[i] == seqNum)
                {
                    return true;
                }
            }

            return false;
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
                var frag = _fragPacketsPool.Get();
                frag.FragId = header.FragId;
                frag.FragCount = header.FragCount;
                _frags.Add(frag);
                return frag;
            }
        }
    }
}
