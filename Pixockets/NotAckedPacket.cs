using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixockets
{
    public class NotAckedPacket
    {
        public int Offset;
        public int Length;
        public byte[] Buffer;
        public int SendTicks;
        public ushort SeqNum;

        public void Strip()
        {
            Buffer = null;
        }
    }
}
