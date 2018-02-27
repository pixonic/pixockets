using Pixockets;
using System.Collections.Generic;
using System.Net;

namespace UnitTests.Mock
{
    public class MockSmartCallbacks : SmartReceiverBase
    {
        public List<IPEndPoint> OnConnectCalls = new List<IPEndPoint>();
        public List<IPEndPoint> OnDisconnectCalls = new List<IPEndPoint>();

        public override void OnConnect(IPEndPoint endPoint)
        {
            OnConnectCalls.Add(endPoint);
        }

        public override void OnDisconnect(IPEndPoint endPoint)
        {
            OnDisconnectCalls.Add(endPoint);
        }
    }
}
