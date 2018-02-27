using System.Net;

namespace Pixockets
{
    // TODO: convert to struct?
    public class ReceivedPacket : IPoolable
    {
        public IPEndPoint EndPoint;
        public int Offset;
        public int Length;
        public byte[] Buffer;

        public void Strip()
        {
            EndPoint = null;
            Buffer = null;
        }
    };

    public class ReceivedSmartPacket
    {
        public IPEndPoint EndPoint;
        public int Offset;
        public int Length;
        public byte[] Buffer;
        public bool InOrder;
    };
}
