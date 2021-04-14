using System.Net;

namespace Pixockets
{
    public abstract class SmartReceiverBase
    {
        public abstract void OnConnect(IPEndPoint endPoint);

        public abstract void OnDisconnect(IPEndPoint endPoint, DisconnectReason reason);
    }

    public class NullSmartReceiver : SmartReceiverBase
    {
        public override void OnConnect(IPEndPoint endPoint)
        {
        }

        public override void OnDisconnect(IPEndPoint endPoint, DisconnectReason reason)
        {
        }
    }
}
