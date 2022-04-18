using System;
using Pixockets;
using System.Collections.Generic;
using System.Net;

namespace UnitTests.Mock
{
    public class MockSmartCallbacks : SmartReceiverBase
    {
        public readonly List<IPEndPoint> OnConnectCalls = new List<IPEndPoint>();
        public readonly List<Tuple<IPEndPoint, DisconnectReason, string>> OnDisconnectCalls = new List<Tuple<IPEndPoint, DisconnectReason, string>>();

        public override void OnConnect(IPEndPoint endPoint)
        {
            OnConnectCalls.Add(endPoint);
        }

        public override void OnDisconnect(IPEndPoint endPoint, DisconnectReason reason, string comment)
        {
            OnDisconnectCalls.Add(new Tuple<IPEndPoint, DisconnectReason, string>(endPoint, reason, comment));
        }
    }
}
