using System;
using System.Collections.Generic;

namespace Pixockets
{
    public class SequenceState
    {
        public ushort SeqNum;
        public List<ushort> Acks = new List<ushort>();

        public void AddAck(ushort ack)
        {
            Acks.Add(ack);
        }
    }
}
