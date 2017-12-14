using System.Net;

namespace Pixockets
{
    public class PacketToSend
    {
        public IPEndPoint EndPoint;
        public int Offset;
        public int Length;
        public byte[] Buffer;
    };
}
