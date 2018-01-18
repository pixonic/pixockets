using Pixockets;
using System.Collections.Generic;
using System.Net;

namespace UnitTests.Mock
{
    public class OnReceiveCall
    {
        public byte[] Buffer;
        public int Offset;
        public int Length;
        public IPEndPoint EndPoint;
        public bool InOrder;
    }

    public class MockCallbacks : ReceiverBase
    {
        public List<OnReceiveCall> OnReceiveCalls = new List<OnReceiveCall>();

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            OnReceiveCalls.Add(new OnReceiveCall
            {
                Buffer = buffer,
                Offset = offset,
                Length = length,
                EndPoint = endPoint,
            });
        }
    }
}
