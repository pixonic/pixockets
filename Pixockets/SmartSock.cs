using System;
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

        public SmartSock(SockBase subSock, SmartReceiverBase callbacks)
        {
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

            var header = new PacketHeader(buffer, offset);
            if (length != header.Length)
            {
                // Wrong packet
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
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            // TODO: move constant to more appropriate place?
            if (length > MaxPayload)
            {
                var seqState = GetSeqState(endPoint);
                ushort fragId = seqState.tNextFragId();
                // Cut packet
                var fragmentCount = (length + MaxPayload - 1) / MaxPayload;
                var tailSize = length;
                for (int i = 0; i < fragmentCount; ++i)
                {
                    var fragmentSize = Math.Min(MaxPayload, tailSize);
                    tailSize -= MaxPayload;
                    var fragmentOffset = offset + i * MaxPayload;

                    var fullBuffer = WrapFragment(endPoint, buffer, fragmentOffset, fragmentSize, fragId, (ushort)i, (ushort)fragmentCount);

                    SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
                }
            }
            else
            {
                var fullBuffer = Wrap(endPoint, buffer, offset, length);

                SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
            }
        }

        public void SendReliable(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            if (length > MaxPayload)
            {
                var seqState = GetSeqState(endPoint);
                ushort fragId = seqState.tNextFragId();
                // Cut packet
                var fragmentCount = (length + MaxPayload - 1) / MaxPayload;
                var tailSize = length;
                for (int i = 0; i < fragmentCount; ++i)
                {
                    var fragmentSize = Math.Min(MaxPayload, tailSize);
                    tailSize -= MaxPayload;
                    var fragmentOffset = offset + i * MaxPayload;

                    var fullBuffer = WrapReliableFragment(endPoint, buffer, fragmentOffset, fragmentSize, fragId, (ushort)i, (ushort)fragmentCount);

                    SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
                }
            }
            else
            {
                var fullBuffer = WrapReliable(endPoint, buffer, offset, length);

                SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
            }
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            var endPoint = SubSock.RemoteEndPoint;
            Send(endPoint, buffer, offset, length);
        }
 
        public void Tick()
        {
            lock (_syncObj)
            {
                var now = Environment.TickCount;
                var toDelete = new List<IPEndPoint>();
                foreach (var seqState in _seqStates)
                {
                    if (TimeDelta(seqState.Value.LastActive, now) > ConnectionTimeout)
                    {
                        toDelete.Add(seqState.Key);
                        continue;
                    }

                    seqState.Value.Tick(seqState.Key, SubSock, now, AckTimeout, FragmentTimeout);
                }

                var toDeleteCount = toDelete.Count;
                for (int i = 0; i < toDeleteCount; ++i)
                {
                    var endPoint = toDelete[i];
                    _seqStates.Remove(endPoint);
                    _callbacks.OnDisconnect(endPoint);
                }
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

        private byte[] Wrap(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            // TODO: pool byte arrays and PacketHeaders
            var header = new PacketHeader();
            header.SetSeqNum(seqNum);

            return AttachHeader(buffer, offset, length, header);
        }

        private byte[] WrapReliable(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            // TODO: pool byte arrays and PacketHeaders
            var header = new PacketHeader();
            header.SetNeedAck();
            header.SetSeqNum(seqNum);

            byte[] fullBuffer = AttachHeader(buffer, offset, length, header);

            AddNotAcked(seqState, seqNum, fullBuffer);

            return fullBuffer;
        }

        private byte[] WrapFragment(IPEndPoint endPoint, byte[] buffer, int offset, int length, ushort fragId, ushort fragNum, ushort fragCount)
        {
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            // TODO: pool byte arrays and PacketHeaders
            var header = new PacketHeader();
            header.SetSeqNum(seqNum);  // TODO: do we really need it?
            header.SetFrag(fragId, fragNum, fragCount);

            return AttachHeader(buffer, offset, length, header);
        }

        private byte[] WrapReliableFragment(IPEndPoint endPoint, byte[] buffer, int offset, int length, ushort fragId, ushort fragNum, ushort fragCount)
        {
            // TODO: pool byte arrays and PacketHeaders
            var header = new PacketHeader();
            header.SetNeedAck();
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            // TODO: pool them
            header.SetSeqNum(seqNum);
            header.SetFrag(fragId, fragNum, fragCount);

            byte[] fullBuffer = AttachHeader(buffer, offset, length, header);

            AddNotAcked(seqState, seqNum, fullBuffer);

            return fullBuffer;
        }

        private static byte[] AttachHeader(byte[] buffer, int offset, int length, PacketHeader header)
        {
            var headLen = header.HeaderLength;
            header.Length = (ushort)(headLen + length);
            var fullBuffer = new byte[length + headLen];
            header.WriteTo(fullBuffer, 0);
            // TODO: find more optimal way
            Array.Copy(buffer, offset, fullBuffer, headLen, length);
            return fullBuffer;
        }

        private void AddNotAcked(SequenceState seqState, ushort seqNum, byte[] fullBuffer)
        {
            var notAcked = _notAckedPool.Get();
            notAcked.Buffer = fullBuffer;
            notAcked.Offset = 0;
            notAcked.Length = fullBuffer.Length;
            notAcked.SendTicks = Environment.TickCount;
            notAcked.SeqNum = seqNum;

            seqState.AddNotAcked(notAcked);
        }

        private void SendAck(IPEndPoint endPoint, ushort seqNum)
        {
            var header = new PacketHeader();
            header.SetAck(seqNum);
            header.Length = (ushort)header.HeaderLength;

            var buffer = new byte[header.Length];
            header.WriteTo(buffer, 0);

            SubSock.Send(endPoint, buffer, 0, buffer.Length);
        }

        private void ReceiveAck(IPEndPoint endPoint, ushort ack)
        {
            var seqState = GetSeqState(endPoint);
            seqState.ReceiveAck(endPoint, ack);
        }

        private SequenceState GetSeqState(IPEndPoint endPoint)
        {
            SequenceState result;
            bool newState = false;
            lock (_syncObj)
            {
                if (!_seqStates.ContainsKey(endPoint))
                {
                    result = new SequenceState(_notAckedPool);
                    _seqStates.Add(endPoint, result);
                    newState = true;
                }
                else
                {
                    result = _seqStates[endPoint];
                }
            }

            if (newState)
            {
                _callbacks.OnConnect(endPoint);
            }

            return result;
        }
    }
}
