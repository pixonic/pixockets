using System.Net;

namespace Pixockets
{
    public abstract class ReceiverBase
    {
        public abstract void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint);
    }
}
