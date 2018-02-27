using System.Net;

namespace Pixockets
{
    public abstract class SmartReceiverBase
    {
        public abstract void OnConnect(IPEndPoint endPoint);

        public abstract void OnDisconnect(IPEndPoint endPoint);
    }
}
