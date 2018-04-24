using System;
using System.Collections.Generic;
using System.Net;

namespace Pixockets
{
    public class SmartSock
    {
        public int ConnectionTimeout = 10000;
        public int AckTimeout = 2000;  // Should be less than max tick
        public int MaxPayload = 1100;  // Shold be less than SubSock.MTU - HeaderLength
        public int FragmentTimeout = 2000;

        private const int DeltaThreshold = 1000000000;

        public IPEndPoint LocalEndPoint { get { return SubSock.LocalEndPoint; } }

        public IPEndPoint RemoteEndPoint { get { return SubSock.RemoteEndPoint; } }

        public readonly SockBase SubSock;

        private readonly Dictionary<IPEndPoint, SequenceState> _seqStates = new Dictionary<IPEndPoint, SequenceState>();
        private readonly SmartReceiverBase _callbacks;
        private readonly BufferPoolBase _buffersPool;
        private readonly Pool<FragmentedPacket> _fragPacketsPool = new Pool<FragmentedPacket>();
        private readonly Pool<SequenceState> _seqStatesPool = new Pool<SequenceState>();
        private readonly Pool<PacketHeader> _headersPool = new Pool<PacketHeader>();

        private readonly List<KeyValuePair<IPEndPoint, SequenceState>> _toDelete = new List<KeyValuePair<IPEndPoint, SequenceState>>();

        public SmartSock(BufferPoolBase buffersPool, SockBase subSock, SmartReceiverBase callbacks)
        {
            _buffersPool = buffersPool;
            SubSock = subSock;
            if (callbacks != null)
            {
                _callbacks = callbacks;
            }
            else
            {
                _callbacks = new NullSmartReceiver();
            }
        }

        public void Connect(IPAddress address, int port)
        {
            SubSock.Connect(address, port);
        }

        public void Listen(int port)
        {
            SubSock.Listen(port);
        }

        public bool Receive(ref ReceivedSmartPacket receivedPacket)
        {
            bool haveResult = false;
            var packet = new ReceivedPacket();
            while (true)
            {
                if (SubSock.Receive(ref packet))
                {
                    haveResult = OnReceive(packet.Buffer, packet.Offset, packet.Length, packet.EndPoint, ref receivedPacket);
                }
                else
                {
                    break;
                }
                if (haveResult)
                {
                    break;
                }
            }
            return haveResult;
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool reliable)
        {
            var seqState = GetSeqState(endPoint);
            if (length > MaxPayload - seqState.AckLoad)
            {
                ushort fragId = seqState.NextFragId();
                // Cut packet
                var fragmentCount = (length + seqState.FullAckLoad + MaxPayload - 1) / MaxPayload;
                var tailSize = length;
                var fragmentOffset = 0;
                for (int i = 0; i < fragmentCount; ++i)
                {
                    var fragmentSize = Math.Min(MaxPayload - seqState.AckLoad, tailSize);
                    tailSize -= fragmentSize;

                    var fullBuffer = WrapFragment(seqState, buffer, fragmentOffset, fragmentSize, fragId, (ushort)i, (ushort)fragmentCount, reliable);
                    // It should be done after using fragmentOffset to cut fragment
                    fragmentOffset += fragmentSize;

                    SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, reliable);
                }
            }
            else
            {
                var fullBuffer = Wrap(seqState, buffer, offset, length, reliable);

                SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, false);
            }
        }

        public void Send(byte[] buffer, int offset, int length, bool reliable)
        {
            var endPoint = SubSock.RemoteEndPoint;
            Send(endPoint, buffer, offset, length, reliable);
        }

        public void Tick()
        {
            var now = Environment.TickCount;
            foreach (var seqState in _seqStates)
            {
                if (TimeDelta(seqState.Value.LastActive, now) > ConnectionTimeout)
                {
                    _toDelete.Add(seqState);
                    continue;
                }

                seqState.Value.Tick(seqState.Key, SubSock, now, AckTimeout, FragmentTimeout);
            }

            var toDeleteCount = _toDelete.Count;
            for (int i = 0; i < toDeleteCount; ++i)
            {
                var seqState = _toDelete[i];
                _seqStates.Remove(seqState.Key);
                _callbacks.OnDisconnect(seqState.Key);
                _seqStatesPool.Put(seqState.Value);
            }

            _toDelete.Clear();
        }

        private bool OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint, ref ReceivedSmartPacket receivedPacket)
        {
            bool haveResult = false;

            // Update activity timestamp on receive packet
            var seqState = GetSeqState(endPoint);
            seqState.LastActive = Environment.TickCount;
            if (seqState.CheckConnected())
            {
                _callbacks.OnConnect(endPoint);
            }

            var header = _headersPool.Get();
            header.Init(buffer, offset);
            if (length != header.Length)
            {
                // Wrong packet
                _headersPool.Put(header);
                return false;
            }

            if ((header.Flags & PacketHeader.ContainsFrag) != 0)
            {
                haveResult = OnReceiveFragment(buffer, offset, length, endPoint, header, ref receivedPacket);
            }
            else if ((header.Flags & PacketHeader.ContainsSeq) != 0)
            {
                bool inOrder = seqState.IsInOrder(header.SeqNum);
                if (inOrder || !seqState.IsDuplicate(header.SeqNum))
                {
                    haveResult = OnReceiveComplete(buffer, offset, length, endPoint, header, inOrder, ref receivedPacket);
                }
                if (inOrder)
                {
                    seqState.RegisterIncoming(header.SeqNum);
                }
            }

            if ((header.Flags & PacketHeader.ContainsAck) != 0)
            {
                ReceiveAck(endPoint, header.Acks);
            }

            if ((header.Flags & PacketHeader.NeedsAck) != 0)
            {
                seqState.EnqueueAck(header.SeqNum);
            }

            _headersPool.Put(header);

            return haveResult;
        }

        private bool OnReceiveComplete(byte[] buffer, int offset, int length, IPEndPoint endPoint, PacketHeader header, bool inOrder, ref ReceivedSmartPacket receivedPacket)
        {
            var headerLen = header.HeaderLength;

            var payloadLength = length - headerLen;
            if (payloadLength > 0)
            {
                receivedPacket.Buffer = buffer;
                receivedPacket.Offset = offset + headerLen;
                receivedPacket.Length = payloadLength;
                receivedPacket.EndPoint = endPoint;
                receivedPacket.InOrder = inOrder;
                return true;
            }

            return false;
        }

        private bool OnReceiveFragment(byte[] buffer, int offset, int length, IPEndPoint endPoint, PacketHeader header, ref ReceivedSmartPacket receivedPacket)
        {
            var seqState = GetSeqState(endPoint);
            seqState.AddFragment(buffer, offset, length, header);

            return seqState.CombineIfFull(header, endPoint, _callbacks, ref receivedPacket);
        }

        // TODO: move it to some common class
        public static int TimeDelta(int t1, int t2)
        {
            var delta = Math.Abs(t1 - t2);
            if (delta > DeltaThreshold)
            {
                delta = Int32.MaxValue - delta;
            }
            return delta;
        }

        private ArraySegment<byte> Wrap(SequenceState seqState, byte[] buffer, int offset, int length, bool reliable)
        {
            ushort seqNum = seqState.NextSeqNum();
            var header = _headersPool.Get();
            if (reliable)
            {
                header.SetNeedAck();
            }
            header.SetSeqNum(seqNum);
            seqState.AddAcks(header);

            var fullBuffer = AttachHeader(buffer, offset, length, header);

            if (reliable)
            {
                AddNotAcked(seqState, seqNum, fullBuffer);
            }

            _headersPool.Put(header);

            return fullBuffer;
        }

        private ArraySegment<byte> WrapFragment(SequenceState seqState, byte[] buffer, int offset, int length, ushort fragId, ushort fragNum, ushort fragCount, bool reliable)
        {
            var header = _headersPool.Get();
            if (reliable)
            {
                header.SetNeedAck();
            }
            ushort seqNum = seqState.NextSeqNum();
            header.SetSeqNum(seqNum);
            header.SetFrag(fragId, fragNum, fragCount);
            seqState.AddAcks(header);

            var fullBuffer = AttachHeader(buffer, offset, length, header);

            if (reliable)
            {
                AddNotAcked(seqState, seqNum, fullBuffer);
            }

            _headersPool.Put(header);

            return fullBuffer;
        }

        private ArraySegment<byte> AttachHeader(byte[] buffer, int offset, int length, PacketHeader header)
        {
            var headLen = header.HeaderLength;
            header.Length = (ushort)(headLen + length);
            var fullLength = length + headLen;
            var fullBuffer = _buffersPool.Get(fullLength);
            header.WriteTo(fullBuffer, 0);
            // TODO: find more optimal way
            Array.Copy(buffer, offset, fullBuffer, headLen, length);
            ArraySegment<byte> result = new ArraySegment<byte>(fullBuffer, 0, fullLength);
            return result;
        }

        private void AddNotAcked(SequenceState seqState, ushort seqNum, ArraySegment<byte> fullBuffer)
        {
            var notAcked = new NotAckedPacket();
            notAcked.Buffer = fullBuffer.Array;
            notAcked.Offset = fullBuffer.Offset;
            notAcked.Length = fullBuffer.Count;
            notAcked.SendTicks = Environment.TickCount;
            notAcked.SeqNum = seqNum;

            seqState.AddNotAcked(notAcked);
        }

        private void EnqueueAck(IPEndPoint endPoint, ushort seqNum)
        {
            var seqState = GetSeqState(endPoint);
            seqState.EnqueueAck(seqNum);
        }

        private void ReceiveAck(IPEndPoint endPoint, List<ushort> acks)
        {
            var seqState = GetSeqState(endPoint);
            seqState.ReceiveAck(acks);
        }

        private SequenceState GetSeqState(IPEndPoint endPoint)
        {
            SequenceState result;
            if (!_seqStates.TryGetValue(endPoint, out result))
            {
                result = _seqStatesPool.Get();
                result.Init(_buffersPool, _fragPacketsPool, _headersPool);
                _seqStates.Add(endPoint, result);
            }

            return result;
        }
    }
}
