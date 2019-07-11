﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Pixockets.Debug;

namespace Pixockets
{
    // WARNING: this class uses Socket.SendTo method which is not working in Unity for iOS
    public class ThreadSock : SockBase
    {
        public Socket SysSock;

        public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }
        public override IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _receiveEndPoint;

        private readonly BufferPoolBase _buffersPool;
        private readonly ILogger _logger;
        private readonly Thread _sendThread;
        private readonly ThreadSafeQueue<PacketToSend> _sendQueue = new ThreadSafeQueue<PacketToSend>();

        private readonly Thread _receiveThread;
        private readonly ThreadSafeQueue<ReceivedPacket> _recvQueue = new ThreadSafeQueue<ReceivedPacket>();

        private readonly object _syncObj = new object();
        private volatile bool _closing;

        private const int SendQueueLimit = 10000;
        private const int RecvQueueLimit = 10000;

        public ThreadSock(BufferPoolBase buffersPool, AddressFamily addressFamily, ILogger logger)
        {
            SysSock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
            _closing = false;

            _buffersPool = buffersPool;
            _logger = logger;
            _sendThread = new Thread(SendLoop);
            _sendThread.IsBackground = true;
            _sendThread.Start();
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
        }

        public override void Connect(IPAddress address, int port)
        {
            if (_closing)
                return;

            _remoteEndPoint = new IPEndPoint(address, port);
            AddressFamily addressFamily;
            lock (_syncObj)
            {
                addressFamily = SysSock.AddressFamily;
                if (SysSock.IsBound)
                {
                    SysSock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
                }
                SysSock.Connect(_remoteEndPoint);
            }

            _receiveEndPoint = new IPEndPoint(AnyAddress(addressFamily), 0);

            if (_receiveThread.ThreadState != ThreadState.Running)
            {
                _receiveThread.Start();
            }
        }

        public override void Listen(int port)
        {
            if (_closing)
                return;

            lock (_syncObj)
            {
                _receiveEndPoint = new IPEndPoint(AnyAddress(SysSock.AddressFamily), port);
                _remoteEndPoint = _receiveEndPoint;
                SysSock.Bind(_remoteEndPoint);
            }

            if (_receiveThread.ThreadState != ThreadState.Running)
            {
                _receiveThread.Start();
            }
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Send(RemoteEndPoint, buffer, offset, length, putBufferToPool);
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            ValidateLength(length);

            var packet = new PacketToSend();
            packet.EndPoint = endPoint;
            packet.Buffer = buffer;
            packet.Offset = offset;
            packet.Length = length;
            packet.PutBufferToPool = putBufferToPool;

            _sendQueue.Add(packet);

            if (_sendQueue.Count > SendQueueLimit)
            {
                // Trash oldest
                if (_sendQueue.TryTake(out packet))
                {
                    if (packet.PutBufferToPool)
                        _buffersPool.Put(packet.Buffer);
                }
            }
        }

        private void SendLoop()
        {
            while (!_closing)
            {
                var packet = _sendQueue.Take();
                try
                {
                    // This seems not implemented in Unity for iOS
                    SysSock.SendTo(packet.Buffer, packet.Offset, packet.Length, SocketFlags.None, packet.EndPoint);
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }
                finally
                {
                    if (packet.PutBufferToPool)
                        _buffersPool.Put(packet.Buffer);
                }
            }
        }

        private void ReceiveLoop()
        {
            while (!_closing)
            {
                var buffer = _buffersPool.Get(MTU);
                var bufferUsed = false;
                EndPoint remoteEP = _receiveEndPoint;
                try
                {
                    var bytesReceived = SysSock.ReceiveFrom(buffer, MTU, SocketFlags.None, ref remoteEP);
                    if (bytesReceived > 0)
                    {
                        var packet = new ReceivedPacket();
                        packet.Buffer = buffer;
                        packet.Offset = 0;
                        packet.Length = bytesReceived;
                        packet.EndPoint = (IPEndPoint)remoteEP;

                        bufferUsed = true;

                        _recvQueue.Add(packet);

                        if (_recvQueue.Count > RecvQueueLimit)
                        {
                            // Trash oldest
                            if (_recvQueue.TryTake(out packet))
                                _buffersPool.Put(packet.Buffer);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Exception(e);
                }

                if (!bufferUsed)
                {
                    _buffersPool.Put(buffer);
                }
            }
        }

        public override bool Receive(ref ReceivedPacket packet)
        {
            return _recvQueue.TryTake(out packet);
        }

        public override void Close()
        {
            _closing = true;
            SysSock.Close();
            _sendThread.Abort();
            _receiveThread.Abort();
            _sendThread.Join();
            if (_receiveThread.IsAlive)
            {
                _receiveThread.Join();
            }
        }
    }
}
