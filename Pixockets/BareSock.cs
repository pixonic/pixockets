using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    public class BareSock
    {
        public Socket SysSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        private SocketAsyncEventArgs eSend = new SocketAsyncEventArgs();
        private SocketAsyncEventArgs eReceive = new SocketAsyncEventArgs();

        public volatile bool ReadyToSend = true;

        private ReceiverBase callbacks;

        ConcurrentQueue<PacketToSend> sendQueue = new ConcurrentQueue<PacketToSend>();

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
            // Strange but works
            eReceive.RemoteEndPoint = SysSock.LocalEndPoint;

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
                ReadyToSend = false;
                eSend.SetBuffer(buffer, offset, length);
                eSend.RemoteEndPoint = endPoint;

                bool willRaiseEvent = SysSock.SendToAsync(eSend);
                if (!willRaiseEvent)
                {
                    OnPacketSent(eSend);
                }
                return;
            }

            sendQueue.Enqueue(new PacketToSend
            {
                EndPoint = endPoint,
                Buffer = buffer,
                Offset = offset,
                Length = length
            });
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
            if (e.LastOperation == SocketAsyncOperation.ReceiveFrom)
            {
                OnPacketReceived(e);
            }
            else if (e.LastOperation == SocketAsyncOperation.ReceiveMessageFrom)
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
            eReceive.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
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
                Send(packet.EndPoint, packet.Buffer, packet.Offset, packet.Length);
            }
            else
            {
                ReadyToSend = true;
            }
        }
    }
}
