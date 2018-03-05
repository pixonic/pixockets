using System.Net;

namespace Pixockets
{
    public struct PacketToSend
    {
        public IPEndPoint EndPoint;
        public int Offset;
        public int Length;
        public byte[] Buffer;
        public bool PutBufferToPool;
    };
}
