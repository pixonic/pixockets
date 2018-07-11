using System;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    public class BareSock : SockBase
    {
        public const int MTU = 1200;
        public Socket SysSock = null;

        public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }

        public override IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _receiveEndPoint;

        private readonly BufferPoolBase _buffersPool;

        public BareSock(BufferPoolBase buffersPool, AddressFamily addressFamily)
        {
            SysSock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
            SysSock.Blocking = false;
            _buffersPool = buffersPool;
        }

        public override void Connect(IPAddress address, int port)
        {
            _remoteEndPoint = new IPEndPoint(address, port);
            AddressFamily addressFamily = SysSock.AddressFamily;
            if (SysSock.IsBound)
            {
                SysSock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
                SysSock.Blocking = false;
            }
            SysSock.Connect(_remoteEndPoint);

            _receiveEndPoint = new IPEndPoint(AnyAddress(addressFamily), 0);
        }

        public override void Listen(int port)
        {
            _receiveEndPoint = new IPEndPoint(AnyAddress(SysSock.AddressFamily), port);
            _remoteEndPoint = _receiveEndPoint;
            SysSock.Bind(_remoteEndPoint);
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            if (length > MTU)
            {
                throw new ArgumentOutOfRangeException(
                    "length", length, string.Format("Length should be less then MTU ({0})", MTU));
            }

            try
            {
                SysSock.SendTo(buffer, offset, length, SocketFlags.None, endPoint);
            }
            catch (Exception)
            {
                // TODO: do something
                return;
            }
            finally
            {
                if (putBufferToPool)
                    _buffersPool.Put(buffer);
            }
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Send(RemoteEndPoint, buffer, offset, length, putBufferToPool);
        }

        public override bool Receive(ref ReceivedPacket packet)
        {
            var buffer = _buffersPool.Get(MTU);
            EndPoint remoteEP = _receiveEndPoint;
            try
            {
                var bytesReceived = SysSock.ReceiveFrom(buffer, MTU, SocketFlags.None, ref remoteEP);
                if (bytesReceived > 0)
                {
                    packet.Buffer = buffer;
                    packet.Offset = 0;
                    packet.Length = bytesReceived;
                    packet.EndPoint = (IPEndPoint)remoteEP;

                    return true;
                }
            }
            catch (Exception)
            {
                // TODO: do something
            }

            _buffersPool.Put(buffer);
            return false;
        }

        public override void Close()
        {
            SysSock.Close();
        }
    }
}
