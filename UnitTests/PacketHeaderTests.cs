using NUnit.Framework;
using Pixockets;
using System;

namespace UnitTests
{
    [TestFixture]
    public class PacketHeaderTests
    {
        [Test]
        public void SerializeHeader()
        {
            var buffer = BitConverter.GetBytes((ushort)54321);
            var header = new PacketHeader(buffer, 0);

            Assert.AreEqual(54321, header.Length);
        }

        [Test]
        public void DeserializeHeader()
        {
            var header = new PacketHeader(54321);

            var buffer = new byte[2];
            header.WriteTo(buffer, 0);

            CollectionAssert.AreEqual(BitConverter.GetBytes((ushort)54321), buffer);
        }
    }
}
