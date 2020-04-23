using System.Net;

namespace Pixockets
{
    public struct ReceivedPacket
    {
        public IPEndPoint EndPoint;
        public int Offset;
        public int Length;
        public byte[] Buffer;
    };

    public struct ReceivedSmartPacket
    {
        public IPEndPoint EndPoint;
        public int Offset;
        public int Length;
        public byte[] Buffer;
        public bool InOrder;
    };
}
