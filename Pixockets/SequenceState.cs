using System;
using System.Collections.Generic;

namespace Pixockets
{
    public class SequenceState
    {
        public ushort SeqNum;
        public readonly List<NotAckedPacket> NotAcked = new List<NotAckedPacket>();
        public int LastActive;

        public SequenceState()
        {
            LastActive = Environment.TickCount;
        }
    }
}
