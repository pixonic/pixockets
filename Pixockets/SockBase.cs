using System;
using System.Net;

namespace Pixockets
{
    public abstract class SockBase
    {
        protected static readonly IPEndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);
        protected static readonly IPEndPoint AnyV6EndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);

        public abstract IPEndPoint LocalEndPoint { get; }
        public abstract IPEndPoint RemoteEndPoint { get; }

        public abstract void Connect(IPAddress address, int port);

        public abstract void Listen(int port);

        public abstract void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool);

        public abstract void Send(byte[] buffer, int offset, int length, bool putBufferToPool);

        public abstract bool Receive(ref ReceivedPacket packet);

        public virtual void Close()
        {
        }
    }
}
