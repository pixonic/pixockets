using System;
using System.Collections.Generic;
using Pixockets.Pools;

namespace Pixockets
{
    public class FragmentedPacket : IPoolable, IEquatable<FragmentedPacket>
    {
        public ushort FragId;
        public ushort FragCount;
        public readonly List<FragmentBuffer> Buffers = new List<FragmentBuffer>();
        public int LastActive;

        public void Strip()
        {
            Buffers.Clear();
        }

        public override bool Equals(object other)
        {
            if (other == null)
                return false;

            var otherPacket = other as FragmentedPacket;
            if (otherPacket == null)
                return false;

            return FragId == otherPacket.FragId;
        }

        public override int GetHashCode()
        {
            return FragId;
        }

        public bool Equals(FragmentedPacket other)
        {
            if (other == null)
                return false;

            return FragId == other.FragId;
        }
    }
}
