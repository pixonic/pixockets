using NUnit.Framework;
using Pixockets;
using System;
using System.IO;

namespace UnitTests
{
    [TestFixture]
    public class PacketHeaderTests
    {
        [Test]
        public void SerializeHeader()
        {
            var header = new PacketHeader(54321);

            var buffer = new byte[PacketHeader.MinHeaderLength];
            header.WriteTo(buffer, 0);

            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)54321), 0, 2);
            ms.WriteByte(0);
            var expectedBuffer = ms.ToArray();

            CollectionAssert.AreEqual(expectedBuffer, buffer);
        }

        [Test]
        public void DeserializeHeader()
        {
            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)54321), 0, 2);
            ms.WriteByte(0);
            var buffer = ms.ToArray();
            var header = new PacketHeader();
            header.Init(buffer, 0);

            Assert.AreEqual(54321, header.Length);
        }

        [Test]
        public void GarbageDeserializationReturnsEmptyPacket()
        {
            TestGarbage(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            TestGarbage(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1 });
        }

        private void TestGarbage(byte[] buffer)
        {
            var header = new PacketHeader();
            header.Init(buffer, 0);

            Assert.AreEqual(0, header.Length);
            Assert.AreEqual(0, header.Flags);
            Assert.AreEqual(0, header.SeqNum);
            Assert.AreEqual(0, header.FragId);
            Assert.AreEqual(0, header.FragNum);
            Assert.AreEqual(0, header.FragCount);
        }
    }
}
