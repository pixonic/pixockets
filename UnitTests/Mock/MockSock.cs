using Pixockets;
using System.Net;
using System;

namespace UnitTests.Mock
{
    class MockSock : SockBase
    {
        public override void SetCallbacks(ReceiverBase callbacks)
        {
        }

        public override void Connect(IPAddress address, int port)
        {
        }

        public override void Receive()
        {
        }

        public override void Receive(int port)
        {
        }

        public override void Send(byte[] buffer, int offset, int length)
        {
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
        }
    }
}
