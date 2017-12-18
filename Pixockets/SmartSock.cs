using System.Net;

namespace Pixockets
{
    public class SmartSock : ReceiverBase
    {
        public readonly BareSock SubSock;

        private ReceiverBase _callbacks;

        public SmartSock(ReceiverBase callbacks)
        {
            SubSock = new BareSock(this);
            _callbacks = callbacks;
        }

        public void Connect(IPAddress address, int port)
        {
            SubSock.Connect(address, port);
        }

        public void ReceiveFrom()
        {
            SubSock.ReceiveFrom();
        }

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            
        }
    }
}
