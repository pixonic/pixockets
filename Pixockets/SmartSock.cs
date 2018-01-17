using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;

namespace Pixockets
{
    public class SmartSock : ReceiverBase
    {
        public int ConnectionTimeout = 10000;
        public int AckTimeout = 1000;
        public int MaxPayload = BareSock.MTU - 100;
        public int FragmentTimeout = 1000;

        private const int DeltaThreshold = 1000000000;

        public IPEndPoint LocalEndPoint { get { return SubSock.LocalEndPoint; } }

        public readonly SockBase SubSock;

        private Dictionary<IPEndPoint, SequenceState> _seqStates = new Dictionary<IPEndPoint, SequenceState>();
        private SmartReceiverBase _callbacks;
        private object _syncObj = new object();
        private readonly Pool<NotAckedPacket> _notAckedPool = new Pool<NotAckedPacket>();
        private readonly ArrayPool<byte> _buffersPool;
        private readonly Pool<FragmentedPacket> _fragPacketsPool = new Pool<FragmentedPacket>();
        private readonly Pool<SequenceState> _seqStatesPool = new Pool<SequenceState>();
        private readonly Pool<PacketHeader> _headersPool = new Pool<PacketHeader>();

        private readonly List<KeyValuePair<IPEndPoint, SequenceState>> _toDelete = new List<KeyValuePair<IPEndPoint, SequenceState>>();

        public SmartSock(ArrayPool<byte> buffersPool, SockBase subSock, SmartReceiverBase callbacks)
        {
            _buffersPool = buffersPool;
            subSock.SetCallbacks(this);
            SubSock = subSock;
            _callbacks = callbacks;
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

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
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
                return;
            }

            if ((header.Flags & PacketHeader.ContainsFrag) != 0)
            {
                OnReceiveFragment(buffer, offset, length, endPoint, header);
            }
            else
            {
                OnReceiveComplete(buffer, offset, length, endPoint, header);
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
            lock (_syncObj)
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
        }

        private void OnReceiveComplete(byte[] buffer, int offset, int length, IPEndPoint endPoint, PacketHeader header)
        {
            var headerLen = header.HeaderLength;

            var payloadLength = length - headerLen;
            if (payloadLength > 0)
            {
                _callbacks.OnReceive(
                    buffer,
                    offset + headerLen,
                    payloadLength,
                    endPoint);
            }
        }

        private void OnReceiveFragment(byte[] buffer, int offset, int length, IPEndPoint endPoint, PacketHeader header)
        {
            var seqState = GetSeqState(endPoint);
            seqState.AddFragment(buffer, offset, length, header);
            seqState.CombineIfFull(header, endPoint, _callbacks);
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
            // TODO: pool byte arrays and PacketHeaders
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
            // TODO: pool byte arrays and PacketHeaders
            var header = _headersPool.Get();
            header.SetNeedAck();
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            // TODO: pool them
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
            var fullBuffer = _buffersPool.Rent(fullLength);
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

            var buffer = _buffersPool.Rent(header.Length);
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
            lock (_syncObj)
            {
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
            }

            return result;
        }
    }
}
