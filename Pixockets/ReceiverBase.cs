using System.Net;

namespace Pixockets
{
    public abstract class ReceiverBase
    {
        public abstract void OnDisconnect(IPEndPoint endPoint);
    }
}
