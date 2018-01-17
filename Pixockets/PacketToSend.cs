using System.Net;

namespace Pixockets
{
    public class PacketToSend : IPoolable
    {
        public IPEndPoint EndPoint;
        public int Offset;
        public int Length;
        public byte[] Buffer;
        public bool PutBufferToPool;

        public void Strip()
        {
            EndPoint = null;
            Buffer = null;
        }
    };
}
