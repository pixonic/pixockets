using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Pixockets.Pools;

namespace Pixockets
{
    public class ThreadSmartSock : SmartReceiverBase
    {
		private readonly PerformanceCounter _requestsCounter;
		private readonly PerformanceCounter _readRequestsCounter;
        private readonly PerformanceCounter _responseCounter;
        private readonly PerformanceCounter _sentResponseCounter;
        private readonly PerformanceCounter[] _typeCounter = new PerformanceCounter[10];

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
            if (PerformanceCounterCategory.Exists("benchmarking"))
            {
                PerformanceCounterCategory.Delete("benchmarking");
            }

            var input = new CounterCreationData("Requests Count Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var read = new CounterCreationData("Read Requests Count Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var output = new CounterCreationData("Sent Response Count Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var send = new CounterCreationData("Response Count Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var join = new CounterCreationData("Join requests Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var leave = new CounterCreationData("Leave requests Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var serView = new CounterCreationData("Serialized view Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var rpc = new CounterCreationData("RPC Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var ping = new CounterCreationData("Ping Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);

            var ackSrv = new CounterCreationData("Need ack from server Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var ackSent = new CounterCreationData("Sent ack Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);

            var ackClnt = new CounterCreationData("Need ack from client Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var ackReceived = new CounterCreationData("Received ack Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);

            var sentFrags = new CounterCreationData("Frags sent Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);
            var recFrags = new CounterCreationData("Frags recieved Per Sec", "", PerformanceCounterType.RateOfCountsPerSecond32);

            var collection = new CounterCreationDataCollection();
            collection.Add(output);
            collection.Add(send);
            collection.Add(input);
            collection.Add(read);
            collection.Add(join);
            collection.Add(leave);
            collection.Add(serView);
            collection.Add(rpc);
            collection.Add(ping);

            collection.Add(ackSrv);
            collection.Add(ackSent);
            collection.Add(ackClnt);
            collection.Add(ackReceived);

            collection.Add(sentFrags);
            collection.Add(recFrags);

            PerformanceCounterCategory.Create("benchmarking", string.Empty,
                PerformanceCounterCategoryType.SingleInstance, collection);
            _requestsCounter = new PerformanceCounter("benchmarking", "Requests Count Per Sec", false);
            _readRequestsCounter = new PerformanceCounter("benchmarking", "Read Requests Count Per Sec", false);
            _responseCounter = new PerformanceCounter("benchmarking", "Response Count Per Sec", false);
            _sentResponseCounter = new PerformanceCounter("benchmarking", "Sent Response Count Per Sec", false);
            _typeCounter[0] = new PerformanceCounter("benchmarking", "Join requests Per Sec", false);
            _typeCounter[4] = new PerformanceCounter("benchmarking", "Leave requests Per Sec", false);
            _typeCounter[7] = new PerformanceCounter("benchmarking", "Serialized view Per Sec", false);
            _typeCounter[8] = new PerformanceCounter("benchmarking", "RPC Per Sec", false);
            _typeCounter[9] = new PerformanceCounter("benchmarking", "Ping Per Sec", false);

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
                _readRequestsCounter.Increment();
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
            _responseCounter.Increment();
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
                        _sentResponseCounter.Increment();
                        if (packetToSend.PutBufferToPool)
                            _buffersPool.Put(packetToSend.Buffer);
                    }

                    ReceivedSmartPacket receivedPacket = new ReceivedSmartPacket();
                    if (_socket.Receive(ref receivedPacket))
                    {
                        active = true;
                        _recvQueue.Add(receivedPacket);
                        _requestsCounter.Increment();
                        var code = receivedPacket.Buffer[receivedPacket.Offset] - 1;
                        if (code >= 0 || code < _typeCounter.Length - 1) _typeCounter[code]?.Increment();
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
