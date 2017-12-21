using System;
using System.Collections.Generic;
using System.Net;

namespace Pixockets
{
    public class SmartSock : ReceiverBase
    {
        public int ConnectionTimeout = 10000;
        public int AckTimeout = 1000;

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

            var headerLen = header.HeaderLength;

            _callbacks.OnReceive(
                buffer,
                offset + headerLen,
                length - headerLen,
                endPoint);

            lock (syncObj)
            {
                if ((header.Flags & PacketHeader.ContainsAck) != 0)
                {
                    ReceiveAck(endPoint, seqState, header.Ack);
                }
            }

            if ((header.Flags & PacketHeader.NeedsAck) != 0)
            {
                SendAck(endPoint, header.SeqNum);
            }
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            var fullBuffer = Wrap(buffer, offset, length);

            SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
        }

        public void SendReliable(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            var fullBuffer = WrapReliable(endPoint, buffer, offset, length);

            SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            var fullBuffer = Wrap(buffer, offset, length);

            SubSock.Send(fullBuffer, offset, fullBuffer.Length);
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

                    var notAcked = seqState.Value.NotAcked;
                    var notAckedCount = notAcked.Count;
                    for (int i = 0; i < notAckedCount; ++i)
                    {
                        var packet = notAcked[i];
                        if (now - packet.SendTicks > AckTimeout)
                        {
                            SubSock.Send(seqState.Key, packet.Buffer, packet.Offset, packet.Length);
                            packet.SendTicks = now;
                        }
                    }
                }
            }
        }

        private static int TimeDelta(int t1, int t2)
        {
            var delta = Math.Abs(t1 - t2);
            if (delta > DeltaThreshold)
            {
                delta = Int32.MaxValue - delta;
            }
            return delta;
        }

        private static byte[] Wrap(byte[] buffer, int offset, int length)
        {
            // TODO: pool byte arrays and PacketHeaders
            var header = new PacketHeader();
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
            var seqNum = seqState.SeqNum++;
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
            seqState.NotAcked.Add(notAcked);

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

        private void ReceiveAck(IPEndPoint endPoint, SequenceState seqState, ushort ack)
        {
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

        private SequenceState GetSeqState(IPEndPoint endPoint)
        {
            if (!_seqStates.ContainsKey(endPoint))
            {
                _seqStates.Add(endPoint, new SequenceState());
            }

            return _seqStates[endPoint];
        }
    }
}
