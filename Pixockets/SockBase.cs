using System;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    public abstract class SockBase
    {
        public const int MTU = 1200;

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

        protected static IPAddress AnyAddress(AddressFamily addressFamily)
        {
            if (addressFamily == AddressFamily.InterNetworkV6)
            {
                return IPAddress.IPv6Any;
            }

            return IPAddress.Any;
        }

        protected static void ValidateLength(int length)
        {
            if (length > MTU)
            {
                throw new ArgumentOutOfRangeException(
                    "length", length, string.Format("Length should be less then MTU ({0})", MTU));
            }
        }
    }
}
