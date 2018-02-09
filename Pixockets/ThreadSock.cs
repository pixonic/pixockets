using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Pixockets
{
    public class ThreadSock : SockBase
    {
        public const int MTU = 1200;
        public Socket SysSock = null;

        public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }
        public override IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

        private static readonly IPEndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private BufferPoolBase _buffersPool;
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _receiveEndPoint;

        private ReceiverBase _callbacks;

        private Thread _sendThread;
        private Pool<PacketToSend> _packetToSendPool = new Pool<PacketToSend>();
        private BlockingCollection<PacketToSend> _sendQueue = new BlockingCollection<PacketToSend>();

        private Thread _receiveThread;
        private Pool<ReceivedPacket> _recvPacketPool = new Pool<ReceivedPacket>();
        private BlockingCollection<ReceivedPacket> _recvQueue = new BlockingCollection<ReceivedPacket>();

        private Thread _cbThread;

        private object _syncObj = new object();


        public ThreadSock(BufferPoolBase buffersPool)
        {
            SysSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            _buffersPool = buffersPool;
            _sendThread = new Thread(new ThreadStart(SendLoop));
            _sendThread.IsBackground = true;
            _sendThread.Start();
            _receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            _receiveThread.IsBackground = true;
            _cbThread = new Thread(new ThreadStart(ReceiveCallbacksLoop));
            _cbThread.IsBackground = true;
            _cbThread.Start();
        }

        public override void Connect(IPAddress address, int port)
        {
            _remoteEndPoint = new IPEndPoint(address, port);
            lock (_syncObj)
            {
                if (SysSock.IsBound)
                {
                    SysSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                }
                SysSock.Connect(_remoteEndPoint);
            }
        }

        public override void Receive()
        {
            _receiveEndPoint = AnyEndPoint;

            if (_receiveThread.ThreadState != ThreadState.Running)
            {
                _receiveThread.Start();
            }
        }

        public override void Receive(int port)
        {
            _receiveEndPoint = new IPEndPoint(IPAddress.Any, port);
            _remoteEndPoint = _receiveEndPoint;
            SysSock.Bind(_remoteEndPoint);

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
            var packet = _packetToSendPool.Get();
            packet.EndPoint = endPoint;
            packet.Buffer = buffer;
            packet.Offset = offset;
            packet.Length = length;
            packet.PutBufferToPool = putBufferToPool;

            _sendQueue.Add(packet);
        }

        public override void SetCallbacks(ReceiverBase callbacks)
        {
            _callbacks = callbacks;
        }

        private void SendLoop()
        {
            while (true)
            {
                var packet = _sendQueue.Take();
                SysSock.SendTo(packet.Buffer, packet.Offset, packet.Length, SocketFlags.None, packet.EndPoint);
                if (packet.PutBufferToPool)
                    _buffersPool.Put(packet.Buffer);
                _packetToSendPool.Put(packet);
            }
        }

        private void ReceiveCallbacksLoop()
        {
            while (true)
            {
                var packet = _recvQueue.Take();
                _callbacks.OnReceive(packet.Buffer, packet.Offset, packet.Length, packet.EndPoint);
                _recvPacketPool.Put(packet);
            }
        }

        private void ReceiveLoop()
        {
            while (true)
            {
                var buffer = _buffersPool.Get(MTU);
                EndPoint remoteEP = _receiveEndPoint;
                try
                {
                    var bytesReceived = SysSock.ReceiveFrom(buffer, ref remoteEP);
                    if (bytesReceived > 0)
                    {
                        var packet = _recvPacketPool.Get();
                        packet.Buffer = buffer;
                        packet.Offset = 0;
                        packet.Length = bytesReceived;
                        packet.EndPoint = (IPEndPoint)remoteEP;

                        _recvQueue.Add(packet);
                    }
                }
                catch (Exception e)
                {
                    // TODO: do something
                    Console.WriteLine("Error sending packet: {0}", e.Message);
                }
            }
        }
    }
}
