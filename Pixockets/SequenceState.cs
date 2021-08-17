using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Pixockets.Pools;

namespace Pixockets
{
    public class SequenceState : IPoolable
    {
        public int LastActive;
        public ushort SessionId;

        private const int ReceivedSeqNumBufferMaxSize = 32;
        private static readonly Random Rnd = new Random();

        private readonly List<NotAckedPacket> _notAcked = new List<NotAckedPacket>();
        private readonly List<FragmentedPacket> _frags = new List<FragmentedPacket>();
        private readonly List<ushort> _ackQueue = new List<ushort>();
        private readonly ushort[] _lastReceivedSeqNums = new ushort[ReceivedSeqNumBufferMaxSize];

        private int _receivedSeqNumBufferSize;
        private bool _connected;
        private ushort _nextSeqNum;
        private ushort _nextFragId;
        private int _lastReceivedSeqNum = -1;  // int for calculations
        private int _lastReceivedSeqNumIdx;

        private Pool<FragmentedPacket> _fragPacketsPool;
        private BufferPoolBase _buffersPool;
        private Pool<PacketHeader> _headersPool;
		private PerformanceCounter _sentAck;

		public int AckLoad
        {
            get { return Math.Min(_ackQueue.Count, 255) * 2; }
        }

        public int FullAckLoad
        {
            get { return _ackQueue.Count * 2; }
        }

        public bool IsConnected
        {
            get { return SessionId != PacketHeader.EmptySessionId; }
        }

        public bool DisconnectRequestSent { get; set; }
        
        public SequenceState()
		{
            _sentAck = new PerformanceCounter("benchmarking", "Sent ack Per Sec", false);
        }

        public void Init(BufferPoolBase buffersPool, Pool<FragmentedPacket> fragPacketsPool, Pool<PacketHeader> headersPool)
        {
            Init(buffersPool, fragPacketsPool, headersPool, (ushort)(Rnd.Next(ushort.MaxValue) + 1));
        }

        public void Init(BufferPoolBase buffersPool, Pool<FragmentedPacket> fragPacketsPool, Pool<PacketHeader> headersPool, ushort sessionId)
        {
            _buffersPool = buffersPool;
            _fragPacketsPool = fragPacketsPool;
            _headersPool = headersPool;
            LastActive = Environment.TickCount;
            SessionId = sessionId;
        }

        public bool CheckConnected()
        {
            if (!_connected && SessionId != PacketHeader.EmptySessionId)
            {
                _connected = true;
                return true;
            }

            return false;
        }

        public ushort NextSeqNum()
        {
            return _nextSeqNum++;
        }

        public ushort NextFragId()
        {
            return _nextFragId++;
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
                _buffersPool.Put(buffer);
            }
        }

        public bool CombineIfFull(PacketHeader header, IPEndPoint endPoint, ref ReceivedSmartPacket receivedPacket)
        {
            int fullLength = 0;

            // TODO: validate that headers of all fragments match
            var frag = GetFragmentedPacket(header);

            if (frag.Buffers.Count < header.FragCount)
            {
                return false;
            }

            var buffersCount = frag.FragCount;
            for (int i = 0; i < buffersCount; ++i)
            {
                fullLength += frag.Buffers[i].Length;
            }

            byte[] combinedBuffer = _buffersPool.Get(fullLength);
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

            // TODO: calculate it?
            bool inOrder = true;

            receivedPacket.Buffer = combinedBuffer;
            receivedPacket.Offset = 0;
            receivedPacket.Length = fullLength;
            receivedPacket.EndPoint = endPoint;
            receivedPacket.InOrder = inOrder;
            return true;
        }

        public void Tick(IPEndPoint endPoint, SockBase sock, int now, int ackTimeout, int fragmentTimeout)
        {
            var notAckedCount = _notAcked.Count;
            for (int i = 0; i < notAckedCount; ++i)
            {
                var packet = _notAcked[i];
                if (now - packet.SendTicks > ackTimeout)
                {
                    sock.Send(endPoint, packet.Buffer, packet.Offset, packet.Length, false);
                    packet.SendTicks = now;
                    _notAcked[i] = packet;
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

            while (_ackQueue.Count > 0)
            {
                var header = _headersPool.Get();
                AddAcks(header);
                header.SetSessionId(SessionId);
                header.Length = (ushort)header.HeaderLength;
                var buffer = _buffersPool.Get(header.Length);
                header.WriteTo(buffer, 0);
                sock.Send(endPoint, buffer, 0, header.Length, true);
                _headersPool.Put(header);
            }
        }

        public void AddAcks(PacketHeader header)
        {
            int acksPerPacket = Math.Min(_ackQueue.Count, 255);
            // TODO: optimize if needed
            for (int i = 0; i < acksPerPacket; i++)
            {
                var seqNum = _ackQueue[i];
                header.AddAck(seqNum);
                _sentAck.Increment();
            }

            _ackQueue.RemoveRange(0, acksPerPacket);
        }

        public void AddNotAcked(NotAckedPacket packet)
        {
            _notAcked.Add(packet);
        }

        public void EnqueueAck(ushort seqNum)
        {
            _ackQueue.Add(seqNum);
        }

        public void ReceiveAck(List<ushort> acks)
        {
            var acksCount = acks.Count;
            for (int i = 0; i < acksCount; ++i)
            {
                var notAckedCount = _notAcked.Count;
                var ack = acks[i];

                for (int j = 0; j < notAckedCount; ++j)
                {
                    var packet = _notAcked[j];
                    if (packet.SeqNum == ack)
                    {
                        _notAcked.RemoveAt(j);
                        _buffersPool.Put(packet.Buffer);
                        break;
                    }
                }
            }
        }

        public void Strip()
        {
            var notAckedCount = _notAcked.Count;
            for (int i = 0; i < notAckedCount; ++i)
            {
                var packet = _notAcked[i];
                _buffersPool.Put(packet.Buffer);
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
            _lastReceivedSeqNumIdx = 0;
            _receivedSeqNumBufferSize = 0;
            DisconnectRequestSent = false;
            Array.Clear(_lastReceivedSeqNums, 0, _lastReceivedSeqNums.Length);
        }

        public void RegisterIncoming(ushort seqNum)
        {
            _lastReceivedSeqNum = seqNum;
            _lastReceivedSeqNums[_lastReceivedSeqNumIdx++] = seqNum;
            _receivedSeqNumBufferSize = Math.Min(_receivedSeqNumBufferSize + 1, ReceivedSeqNumBufferMaxSize);
            if (_lastReceivedSeqNumIdx == ReceivedSeqNumBufferMaxSize)
            {
                _lastReceivedSeqNumIdx = 0;
            }
        }

        public bool IsInOrder(ushort seqNum)
        {
            //TODO: fix more corner cases?
            if (_lastReceivedSeqNum == -1)
            {
                return true;
            }

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
            for (int i = 0; i < _receivedSeqNumBufferSize; ++i)
            {
                if (_lastReceivedSeqNums[i] == seqNum)
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
