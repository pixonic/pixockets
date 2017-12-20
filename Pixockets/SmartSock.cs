using System;
using System.Collections.Generic;
using System.Net;

namespace Pixockets
{
    public class SmartSock : ReceiverBase
    {
        public IPEndPoint LocalEndPoint { get { return SubSock.LocalEndPoint; } }

        public readonly SockBase SubSock;

        private Dictionary<IPEndPoint, SequenceState> _seqStates = new Dictionary<IPEndPoint, SequenceState>();

        private ReceiverBase _callbacks;

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
            var header = new PacketHeader(buffer, offset);
            if (length != header.Length)
            {
                return;
            }

            var headerLen = header.HeaderLength;

            _callbacks.OnReceive(
                buffer,
                offset + headerLen,
                length - headerLen,
                endPoint);

            if ((header.Flags & PacketHeader.NeedsAck) != 0)
            {
                SendAck(endPoint, header.SeqNum);
            }
            //else // Wrong packet
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
            var seqNum = GetSeqState(endPoint).SeqNum++;
            header.SetSeqNum(seqNum);
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
