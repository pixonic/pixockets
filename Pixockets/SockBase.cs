using System.Net;

namespace Pixockets
{
    public abstract class SockBase
    {
        public virtual IPEndPoint LocalEndPoint { get; }

        public abstract void SetCallbacks(ReceiverBase callbacks);

        public abstract void Connect(IPAddress address, int port);

        public abstract void Receive();

        public abstract void Receive(int port);

        public abstract void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length);

        public abstract void Send(byte[] buffer, int offset, int length);
    }
}
