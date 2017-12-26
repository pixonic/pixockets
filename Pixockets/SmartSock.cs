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
        private ReceiverBase _callbacks;
        private object syncObj = new object();

        public SmartSock(SockBase subSock, ReceiverBase callbacks)
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
                byte fragId;
                lock (seqState.SyncObj)
                {
                    fragId = seqState.FragId++;
                }
                // Cut packet
                var fragmentCount = (length + MaxPayload) / MaxPayload;
                var tailSize = length;
                for (int i = 0; i < fragmentCount; ++i)
                {
                    // TODO: pool them
                    var fragmentBuffer = new byte[MaxPayload];
                    Array.Copy(buffer, offset + i * MaxPayload, fragmentBuffer, 0, Math.Min(MaxPayload, tailSize));
                    tailSize -= MaxPayload;

                    var fullBuffer = WrapFragment(endPoint, buffer, offset, length, fragId, (ushort)i, (ushort)fragmentCount);

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
            var fullBuffer = WrapReliable(endPoint, buffer, offset, length);

            SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            var endPoint = SubSock.RemoteEndPoint;
            Send(endPoint, buffer, offset, length);
        }
 
        public void Tick()
        {
            lock (syncObj)
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
                    _seqStates.Remove(toDelete[i]);
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
            var headLen = header.HeaderLength;
            header.Length = (ushort)(headLen + length);
            var fullBuffer = new byte[length + headLen];
            header.WriteTo(fullBuffer, 0);
            // TODO: find more optimal way
            Array.Copy(buffer, offset, fullBuffer, headLen, length);
            return fullBuffer;
        }

        private byte[] WrapReliable(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            // TODO: pool byte arrays and PacketHeaders
            var header = new PacketHeader();
            header.SetNeedAck();
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            // TODO: pool them
            header.SetSeqNum(seqNum);
            var headLen = header.HeaderLength;
            header.Length = (ushort)(headLen + length);
            var fullBuffer = new byte[length + headLen];
            header.WriteTo(fullBuffer, 0);
            // TODO: find more optimal way
            Array.Copy(buffer, offset, fullBuffer, headLen, length);

            var notAcked = new NotAckedPacket();
            notAcked.Buffer = fullBuffer;
            notAcked.Offset = 0;
            notAcked.Length = fullBuffer.Length;
            notAcked.SendTicks = Environment.TickCount;
            notAcked.SeqNum = seqNum;
            lock (seqState.SyncObj)
            {
                seqState.NotAcked.Add(notAcked);
            }
            return fullBuffer;
        }


        private byte[] WrapFragment(IPEndPoint endPoint, byte[] buffer, int offset, int length, byte fragId, ushort fragNum, ushort fragCount)
        {
            var seqState = GetSeqState(endPoint);
            ushort seqNum = seqState.NextSeqNum();
            // TODO: pool byte arrays and PacketHeaders
            var header = new PacketHeader();
            header.SetSeqNum(seqNum);  // TODO: do we really need it?
            header.SetFrag(fragId, fragNum, fragCount);
            var headLen = header.HeaderLength;
            header.Length = (ushort)(headLen + length);
            var fullBuffer = new byte[length + headLen];
            header.WriteTo(fullBuffer, 0);
            // TODO: find more optimal way
            Array.Copy(buffer, offset, fullBuffer, headLen, length);
            return fullBuffer;
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
            lock (syncObj)
            {
                var seqState = GetSeqState(endPoint);
                var notAcked = seqState.NotAcked;
                var notAckedCount = notAcked.Count;
                for (int i = 0; i < notAckedCount; ++i)
                {
                    var packet = notAcked[i];
                    if (packet.SeqNum == ack)
                    {
                        notAcked.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private SequenceState GetSeqState(IPEndPoint endPoint)
        {
            lock (syncObj)
            {
                if (!_seqStates.ContainsKey(endPoint))
                {
                    _seqStates.Add(endPoint, new SequenceState());
                }

                return _seqStates[endPoint];
            }
        }
    }
}
