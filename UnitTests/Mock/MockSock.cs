﻿using Pixockets;
using System.Net;
using System.Collections.Generic;

namespace UnitTests.Mock
{
    class MockSock : SockBase
    {
        public ReceiverBase Callbacks;
        public IPEndPoint ConnectEndPoint;
        public List<PacketToSend> Sends = new List<PacketToSend>();
        public int ReceiveCalls;
        public List<int> ReceiveOnPortCalls = new List<int>();

        public override IPEndPoint LocalEndPoint
        {
            get
            {
                return new IPEndPoint(IPAddress.Any, 0);
            }
        }

        public override IPEndPoint RemoteEndPoint
        {
            get { return ConnectEndPoint; }
        }

        public override void SetCallbacks(ReceiverBase callbacks)
        {
            Callbacks = callbacks;
        }

        public override void Connect(IPAddress address, int port)
        {
            ConnectEndPoint = new IPEndPoint(address, port);
        }

        public override void Receive()
        {
            ReceiveCalls += 1;
        }

        public override void Receive(int port)
        {
            ReceiveOnPortCalls.Add(port);
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Send(ConnectEndPoint, buffer, offset, length, putBufferToPool);
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Sends.Add(new PacketToSend {
                EndPoint = endPoint,
                Buffer = buffer,
                Offset = offset,
                Length = length,
            });
        }
    }
}
