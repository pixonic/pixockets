using Pixockets;
using System.Collections.Generic;
using System.Net;

namespace UnitTests.Mock
{
    public class MockSmartCallbacks : SmartReceiverBase
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
