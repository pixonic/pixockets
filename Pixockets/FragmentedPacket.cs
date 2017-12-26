using System.Collections.Generic;

namespace Pixockets
{
    public class FragmentedPacket
    {
        public byte FragId;
        public List<FragmentBuffer> Buffers = new List<FragmentBuffer>();
    }
}
