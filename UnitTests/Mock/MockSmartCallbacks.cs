using System;
using Pixockets;
using System.Collections.Generic;
using System.Net;

namespace UnitTests.Mock
{
    public class MockSmartCallbacks : SmartReceiverBase
    {
        public List<IPEndPoint> OnConnectCalls = new List<IPEndPoint>();
        public List<Tuple<IPEndPoint, DisconnectReason>> OnDisconnectCalls = new List<Tuple<IPEndPoint, DisconnectReason>>();

        public override void OnConnect(IPEndPoint endPoint)
        {
            OnConnectCalls.Add(endPoint);
        }

        public override void OnDisconnect(IPEndPoint endPoint, DisconnectReason reason)
        {
            OnDisconnectCalls.Add(new Tuple<IPEndPoint, DisconnectReason>(endPoint, reason));
        }
    }
}
