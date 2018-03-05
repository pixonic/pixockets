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

        private Dictionary<IPEndPoint, SequenceState> _seqStates = new Dictionary<IPEndPoint, SequenceState>();
        private SmartReceiverBase _callbacks;
        private readonly Pool<NotAckedPacket> _notAckedPool = new Pool<NotAckedPacket>();
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

        public void Receive()
        {
            SubSock.Receive();
        }

        public void Receive(int port)
        {
            SubSock.Receive(port);
        }

        public bool ReceiveFrom(ref ReceivedSmartPacket receivedPacket)
        {
            bool haveResult = false;
            var packet = new ReceivedPacket();
            while (true)
            {
                if (SubSock.ReceiveFrom(ref packet))
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

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            // TODO: move constant to more appropriate place?
            if (length > MaxPayload)
            {
                var seqState = GetSeqState(endPoint);
                ushort fragId = seqState.NextFragId();
                // Cut packet
                // TODO: avoid trying to send if fragmentCount > 65536
                var fragmentCount = (length + MaxPayload - 1) / MaxPayload;
                var tailSize = length;
                for (int i = 0; i < fragmentCount; ++i)
                {
                    var fragmentSize = Math.Min(MaxPayload, tailSize);
                    tailSize -= MaxPayload;
                    var fragmentOffset = offset + i * MaxPayload;

                    var fullBuffer = WrapFragment(endPoint, buffer, fragmentOffset, fragmentSize, fragId, (ushort)i, (ushort)fragmentCount);

                    SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, true);
                }
            }
            else
            {
                var fullBuffer = Wrap(endPoint, buffer, offset, length);

                SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, true);
            }
        }

        public void SendReliable(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            if (length > MaxPayload)
            {
                var seqState = GetSeqState(endPoint);
                ushort fragId = seqState.NextFragId();
                // Cut packet
                var fragmentCount = (length + MaxPayload - 1) / MaxPayload;
                var tailSize = length;
                for (int i = 0; i < fragmentCount; ++i)
                {
                    var fragmentSize = Math.Min(MaxPayload, tailSize);
                    tailSize -= MaxPayload;
                    var fragmentOffset = offset + i * MaxPayload;

                    var fullBuffer = WrapReliableFragment(endPoint, buffer, fragmentOffset, fragmentSize, fragId, (ushort)i, (ushort)fragmentCount);

                    SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, false);
                }
            }
            else
            {
                var fullBuffer = WrapReliable(endPoint, buffer, offset, length);

                SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, false);
            }
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            var endPoint = SubSock.RemoteEndPoint;
            Send(endPoint, buffer, offset, length);
        }

        public void SendReliable(byte[] buffer, int offset, int length)
        {
            var endPoint = SubSock.RemoteEndPoint;
            SendReliable(endPoint, buffer, offset, length);
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
                ReceiveAck(endPoint, header.Ack);
            }

            if ((header.Flags & PacketHeader.NeedsAck) != 0)
            {
                SendAck(endPoint, header.SeqNum);
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

        private ArraySegment<byte> Wrap(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            var header = _headersPool.Get();
            header.SetSeqNum(seqNum);

            var fullBuffer = AttachHeader(buffer, offset, length, header);

            _headersPool.Put(header);

            return fullBuffer;
        }

        private ArraySegment<byte> WrapReliable(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            var header = _headersPool.Get();
            header.SetNeedAck();
            header.SetSeqNum(seqNum);

            var fullBuffer = AttachHeader(buffer, offset, length, header);

            _headersPool.Put(header);

            AddNotAcked(seqState, seqNum, fullBuffer);

            return fullBuffer;
        }

        private ArraySegment<byte> WrapFragment(IPEndPoint endPoint, byte[] buffer, int offset, int length, ushort fragId, ushort fragNum, ushort fragCount)
        {
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            var header = _headersPool.Get();
            header.SetSeqNum(seqNum);  // TODO: do we really need it?
            header.SetFrag(fragId, fragNum, fragCount);

            var fullBuffer = AttachHeader(buffer, offset, length, header);

            _headersPool.Put(header);

            return fullBuffer;
        }

        private ArraySegment<byte> WrapReliableFragment(IPEndPoint endPoint, byte[] buffer, int offset, int length, ushort fragId, ushort fragNum, ushort fragCount)
        {
            var header = _headersPool.Get();
            header.SetNeedAck();
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            header.SetSeqNum(seqNum);
            header.SetFrag(fragId, fragNum, fragCount);

            var fullBuffer = AttachHeader(buffer, offset, length, header);

            AddNotAcked(seqState, seqNum, fullBuffer);

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
            var notAcked = _notAckedPool.Get();
            notAcked.Buffer = fullBuffer.Array;
            notAcked.Offset = fullBuffer.Offset;
            notAcked.Length = fullBuffer.Count;
            notAcked.SendTicks = Environment.TickCount;
            notAcked.SeqNum = seqNum;

            seqState.AddNotAcked(notAcked);
        }

        private void SendAck(IPEndPoint endPoint, ushort seqNum)
        {
            var header = _headersPool.Get();
            header.SetAck(seqNum);
            header.Length = (ushort)header.HeaderLength;

            var buffer = _buffersPool.Get(header.Length);
            header.WriteTo(buffer, 0);

            SubSock.Send(endPoint, buffer, 0, header.Length, true);

            _headersPool.Put(header);
        }

        private void ReceiveAck(IPEndPoint endPoint, ushort ack)
        {
            var seqState = GetSeqState(endPoint);
            seqState.ReceiveAck(endPoint, ack);
        }

        private SequenceState GetSeqState(IPEndPoint endPoint)
        {
            SequenceState result;
            if (!_seqStates.ContainsKey(endPoint))
            {
                result = _seqStatesPool.Get();
                result.Init(_buffersPool, _fragPacketsPool, _notAckedPool);
                _seqStates.Add(endPoint, result);
            }
            else
            {
                result = _seqStates[endPoint];
            }

            return result;
        }
    }
}
