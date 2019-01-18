using System;
using System.Net.Sockets;

namespace Pixockets
{
    public class BareSockClient : BareSock
    {
        public BareSockClient(BufferPoolBase buffersPool, AddressFamily addressFamily) : base(buffersPool, addressFamily)
        {
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

            var buffer = BuffersPool.Get(MTU);
            try
            {
                var bytesReceived = SysSock.Receive(buffer, MTU, SocketFlags.None);
                if (bytesReceived > 0)
                {
                    packet.Buffer = buffer;
                    packet.Offset = 0;
                    packet.Length = bytesReceived;
                    packet.EndPoint = RemoteEndPoint;

                    return true;
                }
            }
            catch (Exception)
            {
                // TODO: do something
            }

            BuffersPool.Put(buffer);
            return false;
        }
    }
}