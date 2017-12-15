using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    public class BareSock
    {
        private static readonly IPEndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        // TODO: support IPV6
        public Socket SysSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        private SocketAsyncEventArgs eSend = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs eReceive = new SocketAsyncEventArgs();

        private volatile bool ReadyToSend = true;
        private ReceiverBase callbacks;

        ConcurrentQueue<PacketToSend> sendQueue = new ConcurrentQueue<PacketToSend>();
        Pool<PacketToSend> _packetsToSend = new Pool<PacketToSend>();

        public BareSock(ReceiverBase callbacks)
        {
            this.callbacks = callbacks;
            eSend.Completed += OnSendCompleted;
            eReceive.Completed += OnReceiveCompleted;
        }

        public void Connect(int port)
        {
            SysSock.Connect(IPAddress.Loopback, port);
        }

        public void ReceiveFrom()
        {
            eReceive.SetBuffer(new byte[4096], 0, 4096);

            eReceive.RemoteEndPoint = AnyEndPoint;

            Receive();
        }

        public void Receive(int port)
        {
            eReceive.SetBuffer(new byte[4096], 0, 4096);
            eReceive.RemoteEndPoint = new IPEndPoint(IPAddress.Any, port);
            SysSock.Bind(eReceive.RemoteEndPoint);

            Receive();
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            // TODO: make it atomic
            if (ReadyToSend)
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

        private void ActualSend(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            ReadyToSend = false;
            eSend.SetBuffer(buffer, offset, length);
            eSend.RemoteEndPoint = endPoint;

            bool willRaiseEvent = SysSock.SendToAsync(eSend);
            if (!willRaiseEvent)
            {
                OnPacketSent(eSend);
            }
        }

        public void Send(int port, byte[] buffer, int offset, int length)
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, port);

            Send(endPoint, buffer, offset, length);
        }

        public void SendTo(byte[] buffer, int offset, int length)
        {
            Send((IPEndPoint)SysSock.RemoteEndPoint, buffer, offset, length);
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

        private void Receive()
        {
            eReceive.SetBuffer(0, 4096);
            bool willRaiseEvent = SysSock.ReceiveMessageFromAsync(eReceive);
            if (!willRaiseEvent)
            {
                OnPacketReceived(eReceive);
            }
        }

        private void OnPacketReceived(SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred <= 0)
                return;

            callbacks.OnReceive(e.Buffer, 0, e.BytesTransferred, (IPEndPoint)e.RemoteEndPoint);

            Receive();
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
                ReadyToSend = true;
            }
        }
    }
}
