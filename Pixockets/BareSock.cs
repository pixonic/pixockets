using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    public class BareSock
    {
        public const int MTU = 1200;
        private static readonly IPEndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);
        // TODO: support IPV6
        public Socket SysSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        private SocketAsyncEventArgs _eSend = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs _eReceive = new SocketAsyncEventArgs();

        private volatile bool _readyToSend = true;
        private ReceiverBase _callbacks;
        private ConcurrentQueue<PacketToSend> sendQueue = new ConcurrentQueue<PacketToSend>();
        private Pool<PacketToSend> _packetsToSend = new Pool<PacketToSend>();

        public BareSock(ReceiverBase callbacks)
        {
            this._callbacks = callbacks;
            _eSend.Completed += OnSendCompleted;
            _eReceive.Completed += OnReceiveCompleted;
        }

        public void Connect(IPAddress address, int port)
        {
            SysSock.Connect(IPAddress.Loopback, port);
        }

        public void Receive()
        {
            _eReceive.SetBuffer(new byte[MTU], 0, MTU);

            _eReceive.RemoteEndPoint = AnyEndPoint;

            ActualReceive();
        }

        public void Receive(int port)
        {
            _eReceive.SetBuffer(new byte[MTU], 0, MTU);
            _eReceive.RemoteEndPoint = new IPEndPoint(IPAddress.Any, port);
            SysSock.Bind(_eReceive.RemoteEndPoint);

            ActualReceive();
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            if (length > MTU)
            {
                throw new ArgumentOutOfRangeException(
                    "length", length, string.Format("Length should be less then MTU ({0})", MTU));
            }

            // TODO: make it atomic
            if (_readyToSend)
            {
                ActualSend(endPoint, buffer, offset, length);
                return;
            }

            var packet = _packetsToSend.Get();
            packet.EndPoint = endPoint;
            packet.Buffer = buffer;
            packet.Offset = offset;
            packet.Length = length;

            sendQueue.Enqueue(packet);
        }

        public void SendTo(byte[] buffer, int offset, int length)
        {
            Send((IPEndPoint)SysSock.RemoteEndPoint, buffer, offset, length);
        }

        private void ActualSend(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            _readyToSend = false;
            _eSend.SetBuffer(buffer, offset, length);
            _eSend.RemoteEndPoint = endPoint;

            bool willRaiseEvent = SysSock.SendToAsync(_eSend);
            if (!willRaiseEvent)
            {
                OnPacketSent(_eSend);
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
            _eReceive.SetBuffer(0, MTU);
            bool willRaiseEvent = SysSock.ReceiveMessageFromAsync(_eReceive);
            if (!willRaiseEvent)
            {
                OnPacketReceived(_eReceive);
            }
        }

        private void OnPacketReceived(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred <= 0)
                return;

            _callbacks.OnReceive(e.Buffer, 0, e.BytesTransferred, (IPEndPoint)e.RemoteEndPoint);

            ActualReceive();
        }

        private void OnPacketSent(SocketAsyncEventArgs e)
        {
            PacketToSend packet;

            if (sendQueue.TryDequeue(out packet))
            {
                ActualSend(packet.EndPoint, packet.Buffer, packet.Offset, packet.Length);
                _packetsToSend.Put(packet);
            }
            else
            {
                _readyToSend = true;
            }
        }
    }
}
