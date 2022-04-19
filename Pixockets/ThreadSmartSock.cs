using System;
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
        private readonly ThreadSafeQueue<ValueTuple<IPEndPoint, DisconnectReason, string>> _disconnectResponses = new ThreadSafeQueue<ValueTuple<IPEndPoint, DisconnectReason, string>>();
        private readonly ThreadSafeQueue<ValueTuple<IPEndPoint, string>> _disconnectRequests = new ThreadSafeQueue<ValueTuple<IPEndPoint, string>>();
        private readonly ThreadSafeQueue<string> _disconnectAllRequests = new ThreadSafeQueue<string>();
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
            while (_disconnectResponses.TryTake(out var disconnectInfo))
                _callbacks.OnDisconnect(disconnectInfo.Item1, disconnectInfo.Item2, disconnectInfo.Item3);

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

        public void Disconnect(IPEndPoint endPoint = null, string comment = "")
        {
            _disconnectRequests.Add(new ValueTuple<IPEndPoint, string>(endPoint, comment));
        }

        public void DisconnectAll(string comment = "DisconnectAll")
        {
            _disconnectAllRequests.Add(comment);
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

                    if (_disconnectRequests.TryTake(out var disconnectRequest))
                    {
                        active = true;
                        _socket.Disconnect(disconnectRequest.Item1, disconnectRequest.Item2);
                    }

                    if (_disconnectAllRequests.TryTake(out var disconnectAllRequest))
                    {
                        active = true;
                        _socket.DisconnectAll(disconnectAllRequest);
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

        public override void OnDisconnect(IPEndPoint endPoint, DisconnectReason reason, string comment)
        {
            _disconnectResponses.Add(new ValueTuple<IPEndPoint, DisconnectReason, string>(endPoint, reason, comment));
        }
    }
}
