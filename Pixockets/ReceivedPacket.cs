using System.Net;

namespace Pixockets
{
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
}
