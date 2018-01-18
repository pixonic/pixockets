using System.Net;

namespace Pixockets
{
    public abstract class SmartReceiverBase
    {
        public abstract void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint, bool inOrder);

        public abstract void OnConnect(IPEndPoint endPoint);

        public abstract void OnDisconnect(IPEndPoint endPoint);
    }
}
