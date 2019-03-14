using System;
using System.Net;
using System.Net.Sockets;

namespace Pixockets
{
    public class BareSock : SockBase
    {
        public Socket SysSock;

        public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }

        public override IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

        private readonly BufferPoolBase _buffersPool;

        private IPEndPoint _remoteEndPoint;
        private bool _connectedMode;

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
            _connectedMode = true;
        }

        public override void Listen(int port)
        {
            _remoteEndPoint = new IPEndPoint(AnyAddress(SysSock.AddressFamily), port);
            SysSock.Bind(_remoteEndPoint);
            _connectedMode = false;
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            ValidateLength(length);

            try
            {
                SysSock.SendTo(buffer, offset, length, SocketFlags.None, endPoint);
            }
            catch (Exception)
            {
                // TODO: do something
            }
            finally
            {
                if (putBufferToPool)
                    _buffersPool.Put(buffer);
            }
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            try
            {
                // It uses Send instead of SendTo because SendTo seems not implemented in Unity on iOS
                SysSock.Send(buffer, offset, length, SocketFlags.None);
            }
            catch (Exception)
            {
                // TODO: do something
            }
            finally
            {
                if (putBufferToPool)
                    _buffersPool.Put(buffer);
            }
        }

        public override bool Receive(ref ReceivedPacket packet)
        {
            try
            {
                if (SysSock.Available == 0)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            var buffer = _buffersPool.Get(MTU);
            EndPoint remoteEP = _remoteEndPoint;
            try
            {
                int bytesReceived = 0;
                if (_connectedMode)
                    bytesReceived = SysSock.Receive(buffer, MTU, SocketFlags.None);
                else
                    bytesReceived = SysSock.ReceiveFrom(buffer, MTU, SocketFlags.None, ref remoteEP);

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
