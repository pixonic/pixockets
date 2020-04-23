using NUnit.Framework;
using Pixockets;
using System;

namespace UnitTests
{
    [TestFixture]
    public class PacketHeaderTests
    {
        private Random _rnd;

        [SetUp]
        public void Setup()
        {
            // Fix seed for repeatability
            _rnd = new Random(1);
        }

        [Test]
        public void SerializationTest()
        {
            for (int i = 0; i < 16; ++i)
            {
                ushort sessionId = (ushort)_rnd.Next(ushort.MaxValue);
                ushort seqNum = (ushort)_rnd.Next(ushort.MaxValue);
                bool needAck = _rnd.Next(2) == 0;
                bool haveFrag = _rnd.Next(2) == 0;

                var header1 = new PacketHeader();
                header1.SetSessionId(sessionId);
                header1.SetSeqNum(seqNum);
                if (needAck)
                    header1.SetNeedAck();
                if (haveFrag)
                {
                    ushort fragId = (ushort)_rnd.Next(ushort.MaxValue);
                    ushort fragCount = (ushort)(_rnd.Next(ushort.MaxValue) + 1);
                    ushort fragNum = (ushort)_rnd.Next(fragCount);
                    header1.SetFrag(fragId, fragNum, fragCount);
                }
                ushort payloadLength = (ushort)(_rnd.Next(ushort.MaxValue) + 1);
                header1.Length = (ushort)(header1.HeaderLength + payloadLength);

                int offset = _rnd.Next(16);

                var buffer = new byte[header1.HeaderLength + offset];
                header1.WriteTo(buffer, offset);

                var header2 = new PacketHeader();
                header2.Init(buffer, offset);

                Assert.AreEqual(header1.Length, header2.Length);
                Assert.AreEqual(header1.Flags, header2.Flags);
                Assert.AreEqual(header1.SessionId, header2.SessionId);
                Assert.AreEqual(header1.GetNeedAck(), header2.GetNeedAck());
                Assert.AreEqual(header1.HeaderLength, header2.HeaderLength);
            }
        }

        [Test]
        public void GarbageDeserializationReturnsEmptyPacket()
        {
            TestGarbage(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9});
            TestGarbage(new byte[] {9, 8, 7, 6, 5, 4, 3, 2, 1});

            var buffer = new byte[64];
            for (int i = 0; i < 16; ++i)
            {
                _rnd.NextBytes(buffer);
                TestGarbage(buffer);
            }
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
            Assert.AreEqual(0, header.SessionId);
        }
    }
}
