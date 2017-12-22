using System;
using System.Collections.Generic;

namespace Pixockets
{
    public class SequenceState
    {
        public ushort SeqNum;
        public readonly List<NotAckedPacket> NotAcked = new List<NotAckedPacket>();
        public int LastActive;
        public object SyncObj = new object();

        public SequenceState()
        {
            LastActive = Environment.TickCount;
        }
    }
}
