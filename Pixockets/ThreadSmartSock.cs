using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Pixockets.Pools;

namespace Pixockets
{
    public class ThreadSmartSock : SmartReceiverBase
    {
        private readonly SmartSock _socket;
        private volatile bool _closing;
        private readonly Thread _ioThread;
        private readonly ThreadSafeQueue<SmartPacketToSend> _sendQueue = new ThreadSafeQueue<SmartPacketToSend>();
        private readonly ThreadSafeQueue<ReceivedSmartPacket> _recvQueue = new ThreadSafeQueue<ReceivedSmartPacket>();
        private readonly ThreadSafeQueue<IPEndPoint> _connectQueue = new ThreadSafeQueue<IPEndPoint>();
        private readonly ThreadSafeQueue<KeyValuePair<IPEndPoint, DisconnectReason>> _disconnectQueue = new ThreadSafeQueue<KeyValuePair<IPEndPoint, DisconnectReason>>();
        private readonly BufferPoolBase _buffersPool;
        private readonly SmartReceiverBase _callbacks;

        public PixocketState State { get; private set; }

        public ThreadSmartSock(BufferPoolBase buffersPool, SockBase subSock, SmartReceiverBase callbacks)
        {
            _socket = new SmartSock(buffersPool, subSock, this);
            _buffersPool = buffersPool;
            if (callbacks != null)
            {
                _callbacks = callbacks;
            }
            else
            {
                _callbacks = new NullSmartReceiver();
            }

            _ioThread = new Thread(IOLoop);
            _ioThread.IsBackground = true;
        }

        public void Connect(IPAddress address, int port)
        {
            _socket.Connect(address, port);
            _ioThread.Start();
        }

        public void Listen(int port)
        {
            _socket.Listen(port);
            _ioThread.Start();
        }

        public void Tick()
        {
            while (_disconnectQueue.TryTake(out var disconnectPair))
                _callbacks.OnDisconnect(disconnectPair.Key, disconnectPair.Value);

            while (_connectQueue.TryTake(out var connectEndPoint))
                _callbacks.OnConnect(connectEndPoint);
        }

        public bool Receive(ref ReceivedSmartPacket receivedPacket)
        {
            if (_recvQueue.Count > 0)
            {
                receivedPacket = _recvQueue.Take();
                return true;
            }

            return false;
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool reliable, bool putBufferToPool)
        {
            var packet = new SmartPacketToSend();
            packet.EndPoint = endPoint;
            packet.Buffer = buffer;
            packet.Offset = offset;
            packet.Length = length;
            packet.PutBufferToPool = putBufferToPool;
            packet.Reliable = reliable;

            _sendQueue.Add(packet);
        }

        public void Send(byte[] buffer, int offset, int length, bool reliable, bool putBufferToPool)
        {
            IPEndPoint endPoint = _socket.RemoteEndPoint;

            Send(endPoint, buffer, offset, length, reliable, putBufferToPool);
        }

        public void Close()
        {
            _closing = true;
        }

        private void IOLoop()
        {
            while (!_closing)
            {
                try
                {
                    bool active = false;

                    _socket.Tick();
                    State = _socket.State;

                    SmartPacketToSend packetToSend = new SmartPacketToSend();
                    if (_sendQueue.Count > 0)
                    {
                        active = true;

                        packetToSend = _sendQueue.Take();
                        _socket.Send(packetToSend.EndPoint, packetToSend.Buffer, packetToSend.Offset, packetToSend.Length, packetToSend.Reliable);
                        if (packetToSend.PutBufferToPool)
                            _buffersPool.Put(packetToSend.Buffer);
                    }

                    ReceivedSmartPacket receivedPacket = new ReceivedSmartPacket();
                    if (_socket.Receive(ref receivedPacket))
                    {
                        active = true;

                        _recvQueue.Add(receivedPacket);
                    }

                    if (!active)
                        Thread.Sleep(10);
                }
                catch (Exception)
                {
                    _closing = true;
                }
            }

            _socket.Close();
            State = PixocketState.NotConnected;
        }

        public override void OnConnect(IPEndPoint endPoint)
        {
            _connectQueue.Add(endPoint);
        }

        public override void OnDisconnect(IPEndPoint endPoint, DisconnectReason reason)
        {
            _disconnectQueue.Add(new KeyValuePair<IPEndPoint, DisconnectReason>(endPoint, reason));
        }
    }
}
