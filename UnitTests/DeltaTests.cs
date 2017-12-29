using NUnit.Framework;
using Pixockets;

namespace UnitTests
{
    [TestFixture]
    public class DeltaTests
    {
        [Test]
        public void DeltaCommutative()
        {
            var t1 = int.MaxValue - 1;
            var t2 = 1;

            Assert.AreEqual(2, SmartSock.TimeDelta(t1, t2));
            Assert.AreEqual(2, SmartSock.TimeDelta(t2, t1));
        }
    }
}
