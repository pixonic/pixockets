using System;
using System.Net;

namespace Pixockets
{
    public abstract class SockBase
    {
        public abstract IPEndPoint LocalEndPoint { get; }
        public abstract IPEndPoint RemoteEndPoint { get; }

        public abstract void Connect(IPAddress address, int port);

        public abstract void Receive();

        public abstract void Receive(int port);

        public abstract void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool);

        public abstract void Send(byte[] buffer, int offset, int length, bool putBufferToPool);

        public abstract ReceivedPacket ReceiveFrom();

        public virtual void Close()
        {
        }
    }
}
