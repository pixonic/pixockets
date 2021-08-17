﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Pixockets.Pools;

namespace Pixockets
{
    public enum PixocketState
    {
        NotConnected = 0,
        Connecting,
        Connected,
    }

    public enum DisconnectReason
    {
        InitiatedByPeer = 0,
        Timeout,
        SocketClose,
    }

    public class SmartSock
    {
        public int ConnectionTimeout = 10000;
        public int AckTimeout = 2000;  // Should be less than max tick
        public int MaxPayload = 1100;  // Should be less than SubSock.MTU - HeaderLength
        public int FragmentTimeout = 30000;
        public int ConnectRequestResendPeriod = 300;

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

        private int _lastConnectRequestSend;
		private PerformanceCounter _needAckFromServer;
		private PerformanceCounter _needAckFromClient;
		private PerformanceCounter _ackRecieved;
		private PerformanceCounter _sentFrags;
		private PerformanceCounter _recFrags;

		public PixocketState State
        {
            get
            {
                SequenceState localSeqenceState;
                var endPoint = RemoteEndPoint;
                if (endPoint != null && _seqStates.TryGetValue(endPoint, out localSeqenceState))
                {
                    if (localSeqenceState.SessionId == PacketHeader.EmptySessionId)
                        return PixocketState.Connecting;

                    return PixocketState.Connected;
                }

                return PixocketState.NotConnected;
            }
        }

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

            _needAckFromServer = new PerformanceCounter("benchmarking", "Need ack from server Per Sec", false);
            _needAckFromClient = new PerformanceCounter("benchmarking", "Need ack from client Per Sec", false);
            _ackRecieved = new PerformanceCounter("benchmarking", "Received ack Per Sec", false);

            _sentFrags = new PerformanceCounter("benchmarking", "Frags sent Per Sec", false);
            _recFrags = new PerformanceCounter("benchmarking", "Frags recieved Per Sec", false);
        }

        public void Connect(IPAddress address, int port)
        {
            SubSock.Connect(address, port);

            SendConnectionRequest();
        }

        public void Listen(int port)
        {
            SubSock.Listen(port);
        }

        public void Disconnect(IPEndPoint endPoint = null)
        {
            if (endPoint == null)
                endPoint = SubSock.RemoteEndPoint;

            if (!_seqStates.TryGetValue(endPoint, out var seqState))
                return;

            SendDisconnectPacket(endPoint, seqState);

            seqState.DisconnectRequestSent = true;
        }

        public bool Receive(ref ReceivedSmartPacket receivedPacket)
        {
            bool haveResult = false;
            var packet = new ReceivedPacket();
            while (true)
            {
                try
                {
                    packet.EndPoint = null;
                    if (SubSock.Receive(ref packet))
                    {
                        try
                        {
                            haveResult = OnReceive(packet.Buffer, packet.Offset, packet.Length, packet.EndPoint,
                                ref receivedPacket);
                        }
                        catch (SocketException)
                        {
                            haveResult = false;
                            _buffersPool.Put(packet.Buffer);
                            if (packet.EndPoint != null && _seqStates.ContainsKey(packet.EndPoint))
                            {
                                Close(packet.EndPoint, _seqStates[packet.EndPoint]);
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                catch (SocketException)
                {
                    Close();
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
            var seqState = GetSeqStateOnSend(endPoint);
            if (seqState.DisconnectRequestSent)
                return;

            // Don't send until connected
            if (seqState.SessionId == PacketHeader.EmptySessionId)
            {
                return;
            }

            // Reliable packets should wait for ack before going to pool
            var putBufferToPool = !reliable;
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

                    try
                    {
                        SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, putBufferToPool);
                        _sentFrags.Increment();
                    }
                    catch (SocketException)
                    {
                        Close(endPoint, seqState);
                        break;
                    }
                }
            }
            else
            {
                var fullBuffer = Wrap(seqState, buffer, offset, length, reliable);
                try
                {
                    SubSock.Send(endPoint, fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, putBufferToPool);
                }
                catch (SocketException)
                {
                    Close(endPoint, seqState);
                }
            }
        }

        // This function has almost the same code as the version above for performance reasons
        // The only difference is the usage of EndPoint-less version of SubSock.Send
        public void Send(byte[] buffer, int offset, int length, bool reliable)
        {
            var endPoint = SubSock.RemoteEndPoint;
            var seqState = GetSeqStateOnSend(endPoint);
            if (seqState.DisconnectRequestSent)
                return;

            // Don't send until connected
            if (seqState.SessionId == PacketHeader.EmptySessionId)
            {
                return;
            }

            // Reliable packets should wait for ack before going to pool
            var putBufferToPool = !reliable;
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

                    try
                    {
                        SubSock.Send(fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, putBufferToPool);
                        _sentFrags.Increment();
                    }
                    catch (SocketException)
                    {
                        Close(endPoint, seqState);
                        break;
                    }
                }
            }
            else
            {
                var fullBuffer = Wrap(seqState, buffer, offset, length, reliable);

                try
                {
                    SubSock.Send(fullBuffer.Array, fullBuffer.Offset, fullBuffer.Count, putBufferToPool);
                }
                catch (SocketException)
                {
                    Close(endPoint, seqState);
                }
            }
        }

        public void Tick()
        {
            var now = Environment.TickCount;
            if (State == PixocketState.Connecting && TimeDelta(_lastConnectRequestSend, now) >= ConnectRequestResendPeriod)
            {
                SendConnectionRequest();
            }

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
                _callbacks.OnDisconnect(seqState.Key, DisconnectReason.Timeout);
                _seqStatesPool.Put(seqState.Value);
            }

            _toDelete.Clear();
        }

        public void DisconnectAll()
        {
            foreach (var seqState in _seqStates)
            {
                Disconnect(seqState.Key);
            }
        }

        public void Close()
        {
            SubSock.Close();
            foreach (var seqState in _seqStates)
            {
                _callbacks.OnDisconnect(seqState.Key, DisconnectReason.SocketClose);
                _seqStatesPool.Put(seqState.Value);
            }

            _seqStates.Clear();
        }

        private void SendConnectionRequest()
        {
            var endPoint = SubSock.RemoteEndPoint;
            var seqState = GetSeqStateOnSend(endPoint);
            var header = _headersPool.Get();
            header.SetSessionId(seqState.SessionId);
            header.SetConnect();
            header.Length = (ushort)header.HeaderLength;

            var buffer = _buffersPool.Get(header.HeaderLength);
            header.WriteTo(buffer, 0);

            var putBufferToPool = true;

            try
            {
                SubSock.Send(buffer, 0, header.HeaderLength, putBufferToPool);
            }
            catch (SocketException)
            {
                Close(endPoint, seqState);
            }

            _headersPool.Put(header);

            _lastConnectRequestSend = Environment.TickCount;
        }


        private void SendConnectionResponse(IPEndPoint endPoint, SequenceState seqState)
        {
            var header = _headersPool.Get();
            header.SetSessionId(seqState.SessionId);
            header.SetConnect();
            header.Length = (ushort)header.HeaderLength;

            var buffer = _buffersPool.Get(header.HeaderLength);
            header.WriteTo(buffer, 0);

            var putBufferToPool = true;

            try
            {
                SubSock.Send(endPoint, buffer, 0, header.HeaderLength, putBufferToPool);
            }
            catch (SocketException)
            {
                Close(endPoint, seqState);
            }
            
            _headersPool.Put(header);
        }

        private bool OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint, ref ReceivedSmartPacket receivedPacket)
        {
            bool haveResult = false;

            var header = _headersPool.Get();
            header.Init(buffer, offset);

            var seqState = GetSeqStateOnReceive(endPoint, header);

            if (length != header.Length || seqState == null)
            {
                // Wrong packet
                _headersPool.Put(header);
                _buffersPool.Put(buffer);
                return false;
            }

            // Update activity timestamp on receive packet
            seqState.LastActive = Environment.TickCount;
            if (seqState.CheckConnected())
            {
                // Send response only for request
                if (header.SessionId == PacketHeader.EmptySessionId)
                    SendConnectionResponse(endPoint, seqState);

                _callbacks.OnConnect(endPoint);
            }
            else if (header.SessionId == PacketHeader.EmptySessionId && (header.Flags & PacketHeader.Connect) != 0)
            {
                SendConnectionResponse(endPoint, seqState);
            }

            if (!seqState.IsConnected)
            {
                // Wrong packet
                _headersPool.Put(header);
                _buffersPool.Put(buffer);
                return false;
            }

            if ((header.Flags & PacketHeader.ContainsFrag) != 0)
            {
                bool isDuplicate = seqState.IsDuplicate(header.SeqNum);
                if (!isDuplicate)
                {
                    haveResult = OnReceiveFragment(buffer, offset, length, endPoint, header, ref receivedPacket);
                    seqState.RegisterIncoming(header.SeqNum);
                    _recFrags.Increment();
                }
            }
            else if ((header.Flags & PacketHeader.ContainsSeq) != 0)
            {
                bool isDuplicate = seqState.IsDuplicate(header.SeqNum);
                if (!isDuplicate)
                {
                    bool inOrder = seqState.IsInOrder(header.SeqNum);
                    haveResult = OnReceiveComplete(buffer, offset, length, endPoint, header, inOrder, ref receivedPacket);
                    seqState.RegisterIncoming(header.SeqNum);
                }
            }

            if ((header.Flags & PacketHeader.Disconnect) != 0)
            {
                // Disconnect request received, send response
                if (!seqState.DisconnectRequestSent)
                {
                    SendDisconnectPacket(endPoint, seqState);
                }

                _callbacks.OnDisconnect(endPoint, DisconnectReason.InitiatedByPeer);
                _seqStates.Remove(endPoint);
                _seqStatesPool.Put(seqState);
            }

            if ((header.Flags & PacketHeader.ContainsAck) != 0)
            {
                seqState.ReceiveAck(header.Acks);
                _ackRecieved.IncrementBy(header.Acks.Count);
            }

            if ((header.Flags & PacketHeader.NeedsAck) != 0)
            {
                seqState.EnqueueAck(header.SeqNum);
                _needAckFromServer.Increment();
            }

            if (!haveResult && (header.Flags & PacketHeader.ContainsFrag) == 0)
            {
                _buffersPool.Put(buffer);
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
            var seqState = GetSeqStateOnReceive(endPoint, header);
            seqState.AddFragment(buffer, offset, length, header);

            return seqState.CombineIfFull(header, endPoint, ref receivedPacket);
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
            header.SetSessionId(seqState.SessionId);
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
                _needAckFromClient.Increment();
            }

            _headersPool.Put(header);

            return fullBuffer;
        }

        private ArraySegment<byte> WrapFragment(SequenceState seqState, byte[] buffer, int offset, int length, ushort fragId, ushort fragNum, ushort fragCount, bool reliable)
        {
            var header = _headersPool.Get();
            header.SetSessionId(seqState.SessionId);
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
                _needAckFromClient.Increment();
            }

            _headersPool.Put(header);

            return fullBuffer;
        }

        private ArraySegment<byte> AttachHeader(byte[] buffer, int offset, int length, PacketHeader header)
        {
            var headLen = header.HeaderLength;
            var fullLength = length + headLen;
            header.Length = (ushort)fullLength;
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

        private SequenceState GetSeqStateOnSend(IPEndPoint endPoint)
        {
            SequenceState result;
            if (!_seqStates.TryGetValue(endPoint, out result))
            {
                result = _seqStatesPool.Get();
                result.Init(_buffersPool, _fragPacketsPool, _headersPool, PacketHeader.EmptySessionId);
                _seqStates.Add(endPoint, result);
            }

            return result;
        }

        private SequenceState GetSeqStateOnReceive(IPEndPoint endPoint, PacketHeader header)
        {
            SequenceState seqState;
            if (!_seqStates.TryGetValue(endPoint, out seqState))
            {
                if (header.SessionId == PacketHeader.EmptySessionId && (header.Flags & PacketHeader.Connect) != 0)
                {
                    seqState = _seqStatesPool.Get();
                    seqState.Init(_buffersPool, _fragPacketsPool, _headersPool);
                    _seqStates.Add(endPoint, seqState);
                }
            }
            else if (seqState.SessionId != header.SessionId)
            {
                if (seqState.SessionId == PacketHeader.EmptySessionId)
                {
                    seqState.SessionId = header.SessionId;
                }
                else if (header.SessionId != PacketHeader.EmptySessionId)
                {
                    return null;
                }
            }

            return seqState;
        }

        private void SendDisconnectPacket(IPEndPoint endPoint, SequenceState seqState)
        {
            var header = _headersPool.Get();
            header.SetSessionId(seqState.SessionId);
            header.SetDisconnect();
            header.Length = (ushort)header.HeaderLength;

            var fullBuffer = _buffersPool.Get(header.Length);
            header.WriteTo(fullBuffer, 0);

            try
            {
                var putBufferToPool = true;
                SubSock.Send(endPoint, fullBuffer, 0, header.Length, putBufferToPool);
            }
            catch (SocketException)
            {
                Close(endPoint, seqState);
            }

            _headersPool.Put(header);
        }

        private void Close(IPEndPoint endPoint, SequenceState seqState)
        {
            _seqStates.Remove(endPoint);
            _callbacks.OnDisconnect(endPoint, DisconnectReason.SocketClose);
            _seqStatesPool.Put(seqState);
        }
    }
}
