using System.Net;
using System.Threading;
using Pixockets.Pools;

namespace Pixockets
{
    public class ThreadSmartSock
    {
        private readonly SmartSock _socket;
        private readonly object _syncObject = new object();
        private volatile bool _closing;
        private readonly Thread _ioThread;
        private readonly ThreadSafeQueue<SmartPacketToSend> _sendQueue = new ThreadSafeQueue<SmartPacketToSend>();
        private readonly ThreadSafeQueue<ReceivedSmartPacket> _recvQueue = new ThreadSafeQueue<ReceivedSmartPacket>();
        private readonly BufferPoolBase _buffersPool;

        public PixocketState State
        {
            get
            {
                lock (_syncObject)
                {
                    return _socket.State;
                }
            }
        }

        public ThreadSmartSock(SmartSock socket, BufferPoolBase buffersPool)
        {
            _socket = socket;
            _buffersPool = buffersPool;

            _ioThread = new Thread(IOLoop);
            _ioThread.IsBackground = true;
            _ioThread.Start();
        }

        public void Connect(IPAddress address, int port)
        {
            lock (_syncObject)
            {
                _socket.Connect(address, port);
            }
        }

        public void Listen(int port)
        {
            lock (_syncObject)
            {
                _socket.Listen(port);
            }
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
            IPEndPoint endPoint;
            lock (_syncObject)
            {
                endPoint = _socket.RemoteEndPoint;
            }

            Send(endPoint, buffer, offset, length, reliable, putBufferToPool);
        }

        public void Close()
        {
            _closing = true;
            lock (_syncObject)
            {
                _socket.Close();
            }
        }

        private void IOLoop()
        {
            bool active = false;
            while (!_closing)
            {
                lock (_syncObject)
                {
                    _socket.Tick();
                }

                SmartPacketToSend packetToSend = new SmartPacketToSend();
                if (_sendQueue.Count > 0)
                {
                    active = true;

                    packetToSend = _sendQueue.Take();
                    lock (_syncObject)
                    {
                        _socket.Send(packetToSend.EndPoint, packetToSend.Buffer, packetToSend.Offset, packetToSend.Length, packetToSend.Reliable);
                    }
                    if (packetToSend.PutBufferToPool)
                        _buffersPool.Put(packetToSend.Buffer);
                }

                ReceivedSmartPacket receivedPacket = new ReceivedSmartPacket();
                bool received;
                lock (_syncObject)
                {
                    received = _socket.Receive(ref receivedPacket);
                }
                if (received)
                {
                    active = true;

                    _recvQueue.Add(receivedPacket);
                }

                if (!active)
                    Thread.Sleep(10);
            }
        }
    }
}
