using System.Net;

namespace Pixockets
{
    public abstract class SmartReceiverBase
    {
        public abstract void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint);
    }
}
