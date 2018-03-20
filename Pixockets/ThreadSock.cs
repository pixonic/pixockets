using System;
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

        private readonly BufferPoolBase _buffersPool;
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _receiveEndPoint;

        private readonly Thread _sendThread;
        private readonly ThreadSafeQueue<PacketToSend> _sendQueue = new ThreadSafeQueue<PacketToSend>();

        private readonly Thread _receiveThread;
        private readonly ThreadSafeQueue<ReceivedPacket> _recvQueue = new ThreadSafeQueue<ReceivedPacket>();

        private readonly object _syncObj = new object();


        public ThreadSock(BufferPoolBase buffersPool)
        {
            SysSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            _buffersPool = buffersPool;
            _sendThread = new Thread(new ThreadStart(SendLoop));
            _sendThread.IsBackground = true;
            _sendThread.Start();
            _receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            _receiveThread.IsBackground = true;
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

            _receiveEndPoint = AnyEndPoint;

            if (_receiveThread.ThreadState != ThreadState.Running)
            {
                _receiveThread.Start();
            }
        }

        public override void Listen(int port)
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
            var packet = new PacketToSend();
            packet.EndPoint = endPoint;
            packet.Buffer = buffer;
            packet.Offset = offset;
            packet.Length = length;
            packet.PutBufferToPool = putBufferToPool;

            _sendQueue.Add(packet);
        }

        private void SendLoop()
        {
            while (true)
            {
                var packet = _sendQueue.Take();
                SysSock.SendTo(packet.Buffer, packet.Offset, packet.Length, SocketFlags.None, packet.EndPoint);
                if (packet.PutBufferToPool)
                    _buffersPool.Put(packet.Buffer);
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
                        var packet = new ReceivedPacket();
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

        public override bool ReceiveFrom(ref ReceivedPacket packet)
        {
            return _recvQueue.TryTake(out packet);
        }

        public override void Close()
        {
            _sendThread.Abort();
            _receiveThread.Abort();
        }
    }
}
