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
            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes((ushort)54321), 0, 2);
            ms.WriteByte(0);
            var buffer = ms.ToArray();
            var header = new PacketHeader();
            header.Init(buffer, 0);

            Assert.AreEqual(54321, header.Length);
        }

        [Test]
        public void DeserializeHeader()
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
    }
}
