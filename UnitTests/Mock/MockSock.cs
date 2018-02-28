using Pixockets;
using System.Net;
using System.Collections.Generic;
using System;

namespace UnitTests.Mock
{
    class MockSock : SockBase
    {
        public IPEndPoint ConnectEndPoint;
        public List<PacketToSend> Sends = new List<PacketToSend>();
        public List<ReceivedPacket> Recvs = new List<ReceivedPacket>();
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

        public override ReceivedPacket ReceiveFrom()
        {
            if (Recvs.Count > 0)
            {
                var result = Recvs[0];
                Recvs.RemoveAt(0);
                return result;
            }

            return null;
        }

        public void FakeReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            Recvs.Add(new ReceivedPacket {
                EndPoint = endPoint,
                Buffer = buffer,
                Offset = offset,
                Length = length,
            });
        }
    }
}
