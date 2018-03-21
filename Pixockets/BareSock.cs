using System;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    // TODO: support IPV6
    public class BareSock : SockBase
    {
        public const int MTU = 1200;
        public Socket SysSock = null;

        public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }

        public override IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _receiveEndPoint;

        private readonly BufferPoolBase _buffersPool;
        private readonly SAEAPool _sendArgsPool = new SAEAPool();
        private readonly SAEAPool _recvArgsPool = new SAEAPool();

        private readonly SendOptions _returnToPool = new SendOptions { ReturnBufferToPool = true };
        private readonly SendOptions _dontReturnToPool = new SendOptions { ReturnBufferToPool = false };
        private readonly ThreadSafeQueue<ReceivedPacket> _receivedQueue = new ThreadSafeQueue<ReceivedPacket>();

        private object _syncObj = new object();

        public BareSock(BufferPoolBase buffersPool, AddressFamily addressFamily)
        {
            SysSock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
            _buffersPool = buffersPool;
        }

        public override void Connect(IPAddress address, int port)
        {
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

            if (addressFamily == AddressFamily.InterNetwork)
            {
                _receiveEndPoint = AnyEndPoint;
            }
            else if (addressFamily == AddressFamily.InterNetworkV6)
            {
                _receiveEndPoint = AnyV6EndPoint;
            }

            var eReceive = GetRecvArgs();
            ActualReceive(eReceive);
        }

        public override void Listen(int port)
        {
            lock (_syncObj)
            {
                if (SysSock.AddressFamily == AddressFamily.InterNetwork)
                {
                    _receiveEndPoint = new IPEndPoint(IPAddress.Any, port);
                }
                else if (SysSock.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    _receiveEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
                }
            }

            _remoteEndPoint = _receiveEndPoint;
            SysSock.Bind(_remoteEndPoint);

            var eReceive = GetRecvArgs();
            ActualReceive(eReceive);
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            if (length > MTU)
            {
                throw new ArgumentOutOfRangeException(
                    "length", length, string.Format("Length should be less then MTU ({0})", MTU));
            }

            ActualSend(endPoint, buffer, offset, length, putBufferToPool);
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Send(RemoteEndPoint, buffer, offset, length, putBufferToPool);
        }

        public override bool Receive(ref ReceivedPacket packet)
        {
            return _receivedQueue.TryTake(out packet);
        }

        private void ActualSend(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            // TODO: refactor
            var eSend = _sendArgsPool.Get();
            if (eSend == null)
            {
                eSend = new SocketAsyncEventArgs();
                eSend.Completed += OnSendCompleted;
            }

            eSend.SetBuffer(buffer, offset, length);
            eSend.RemoteEndPoint = endPoint;
            if (putBufferToPool)
            {
                eSend.UserToken = _returnToPool;
            }
            else
            {
                eSend.UserToken = _dontReturnToPool;
            }
            bool willRaiseEvent;
            lock (_syncObj)
            {
                willRaiseEvent = SysSock.SendToAsync(eSend);
            }
            if (!willRaiseEvent)
            {
                OnPacketSent(eSend);
            }
        }

        private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.SendTo)
            {
                OnPacketSent(e);
            }
            else
            {
                Console.WriteLine("Unexpected operation completed");
            }
        }

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            var eReceive = GetRecvArgs();
            ActualReceive(eReceive);

            if (e.LastOperation == SocketAsyncOperation.ReceiveMessageFrom)
            {
                OnPacketReceived(e);
            }
            else
            {
                Console.WriteLine("Unexpected operation completed");
            }
        }

        private SocketAsyncEventArgs GetRecvArgs()
        {
            var eReceive = _recvArgsPool.Get();
            if (eReceive == null)
            {
                eReceive = new SocketAsyncEventArgs();
                eReceive.Completed += OnReceiveCompleted;
            }
            eReceive.RemoteEndPoint = _remoteEndPoint;
            var buffer = _buffersPool.Get(MTU);
            eReceive.SetBuffer(buffer, 0, MTU);
            return eReceive;
        }

        private void ActualReceive(SocketAsyncEventArgs recvArgs)
        {
            bool willRaiseEvent;
            lock (_syncObj)
            {
                if (SysSock == null)
                {
                    _recvArgsPool.Put(recvArgs);
                    return;
                }

                recvArgs.RemoteEndPoint = _receiveEndPoint;
                willRaiseEvent = SysSock.ReceiveMessageFromAsync(recvArgs);
            }

            if (!willRaiseEvent)
            {
                var eReceive = GetRecvArgs();
                ActualReceive(eReceive);

                OnPacketReceived(recvArgs);
            }
        }

        private void OnPacketReceived(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0)
            {
                var ep = (IPEndPoint)e.RemoteEndPoint;

                var received = new ReceivedPacket();
                received.Buffer = e.Buffer;
                received.Offset = 0;
                received.Length = e.BytesTransferred;
                received.EndPoint = ep;

                _receivedQueue.Add(received);
            }

            _recvArgsPool.Put(e);
        }

        private void OnPacketSent(SocketAsyncEventArgs e)
        {
            var sendOptions = (SendOptions)e.UserToken;

            if (sendOptions.ReturnBufferToPool)
            {
                _buffersPool.Put(e.Buffer);
            }
            _sendArgsPool.Put(e);
        }
    }
}
