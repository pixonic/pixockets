using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    public class BareSock : SockBase
    {
        public const int MTU = 1200;
        // TODO: support IPV6
        public Socket SysSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }
        public override IPEndPoint RemoteEndPoint { get { return (IPEndPoint)SysSock.RemoteEndPoint; } }

        private static readonly IPEndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private ArrayPool<byte> _buffersPool;
        private SocketAsyncEventArgs _eReceive = new SocketAsyncEventArgs();

        private volatile bool _readyToSend = true;
        private ReceiverBase _callbacks;
        private ConcurrentQueue<PacketToSend> _sendQueue = new ConcurrentQueue<PacketToSend>();
        private Pool<PacketToSend> _packetsToSendPool = new Pool<PacketToSend>();
        private SAEAPool _sendArgsPool = new SAEAPool();

        private SendOptions _returnToPool = new SendOptions { ReturnBufferToPool = true };
        private SendOptions _dontReturnToPool = new SendOptions { ReturnBufferToPool = false };

        public BareSock(ArrayPool<byte> buffersPool)
        {
            _buffersPool = buffersPool;
            _eReceive.Completed += OnReceiveCompleted;
        }

        public override void SetCallbacks(ReceiverBase callbacks)
        {
            _callbacks = callbacks;
        }

        public override void Connect(IPAddress address, int port)
        {
            SysSock.Connect(IPAddress.Loopback, port);
        }

        public override void Receive()
        {
            _eReceive.SetBuffer(_buffersPool.Rent(MTU), 0, MTU);

            _eReceive.RemoteEndPoint = AnyEndPoint;

            ActualReceive();
        }

        public override void Receive(int port)
        {
            _eReceive.RemoteEndPoint = new IPEndPoint(IPAddress.Any, port);
            SysSock.Bind(_eReceive.RemoteEndPoint);

            ActualReceive();
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            if (length > MTU)
            {
                throw new ArgumentOutOfRangeException(
                    "length", length, string.Format("Length should be less then MTU ({0})", MTU));
            }

            // TODO: make it atomic
            if (_readyToSend)
            {
                ActualSend(endPoint, buffer, offset, length, putBufferToPool);
                return;
            }

            var packet = _packetsToSendPool.Get();
            packet.EndPoint = endPoint;
            packet.Buffer = buffer;
            packet.Offset = offset;
            packet.Length = length;
            packet.PutBufferToPool = putBufferToPool;

            _sendQueue.Enqueue(packet);
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Send((IPEndPoint)SysSock.RemoteEndPoint, buffer, offset, length, putBufferToPool);
        }

        private void ActualSend(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            _readyToSend = false;
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

            bool willRaiseEvent = SysSock.SendToAsync(eSend);
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
            if (e.LastOperation == SocketAsyncOperation.ReceiveMessageFrom)
            {
                OnPacketReceived(e);
            }
            else
            {
                Console.WriteLine("Unexpected operation completed");
            }
        }

        private void ActualReceive()
        {
            _eReceive.SetBuffer(_buffersPool.Rent(MTU), 0, MTU);
            bool willRaiseEvent = SysSock.ReceiveMessageFromAsync(_eReceive);
            if (!willRaiseEvent)
            {
                OnPacketReceived(_eReceive);
            }
        }

        private void OnPacketReceived(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0)
            {
                _callbacks.OnReceive(e.Buffer, 0, e.BytesTransferred, (IPEndPoint)e.RemoteEndPoint);
            }

            ActualReceive();
        }

        private void OnPacketSent(SocketAsyncEventArgs e)
        {
            PacketToSend packet;
            
            var sendOptions = (SendOptions)e.UserToken;

            if (sendOptions.ReturnBufferToPool)
            {
                _buffersPool.Return(e.Buffer);
            }
            _sendArgsPool.Put(e);

            if (_sendQueue.TryDequeue(out packet))
            {
                ActualSend(packet.EndPoint, packet.Buffer, packet.Offset, packet.Length, packet.PutBufferToPool);
                _packetsToSendPool.Put(packet);
            }
            else
            {
                _readyToSend = true;
            }
        }
    }
}
