using System.Collections.Generic;

namespace Pixockets
{
    public class FragmentedPacket
    {
        public ushort FragId;
        public List<FragmentBuffer> Buffers = new List<FragmentBuffer>();
        public int LastActive;
    }
}
