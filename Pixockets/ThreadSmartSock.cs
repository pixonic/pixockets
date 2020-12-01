using System.Net;
using System.Threading;
using Pixockets.Pools;

namespace Pixockets
{
    public class ThreadSmartSock
    {
        private readonly SmartSock _socket;
        private volatile bool _closing;
        private readonly Thread _ioThread;
        private readonly ThreadSafeQueue<SmartPacketToSend> _sendQueue = new ThreadSafeQueue<SmartPacketToSend>();
        private readonly ThreadSafeQueue<ReceivedSmartPacket> _recvQueue = new ThreadSafeQueue<ReceivedSmartPacket>();
        private readonly BufferPoolBase _buffersPool;

        public PixocketState State { get; private set; }

        public ThreadSmartSock(SmartSock socket, BufferPoolBase buffersPool)
        {
            _socket = socket;
            _buffersPool = buffersPool;

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

            _socket.Close();
        }
    }
}
