﻿using Pixockets;
using System.Net;
using System.Collections.Generic;

namespace UnitTests.Mock
{
    class MockSock : SockBase
    {
        public IPEndPoint ConnectEndPoint;
        public List<PacketToSend> Sends = new List<PacketToSend>();
        public List<ReceivedPacket> Recvs = new List<ReceivedPacket>();
        public List<int> ReceiveOnPortCalls = new List<int>();

        public PacketToSend LastSend
        {
            get { return Sends[Sends.Count - 1]; }
        }

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

        public override void Listen(int port)
        {
            ReceiveOnPortCalls.Add(port);
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            ValidateLength(length);

            Send(ConnectEndPoint, buffer, offset, length, putBufferToPool);
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Sends.Add(new PacketToSend {
                EndPoint = endPoint,
                Buffer = buffer,
                Offset = offset,
                Length = length,
                PutBufferToPool = putBufferToPool,
            });
        }

        public override bool Receive(ref ReceivedPacket packet)
        {
            if (Recvs.Count > 0)
            {
                packet = Recvs[0];
                Recvs.RemoveAt(0);
                return true;
            }

            return false;
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
