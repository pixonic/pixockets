using System;
using System.Net;

namespace Pixockets
{
    public class SmartSock : ReceiverBase
    {
        // TODO: invert dependency
        public readonly BareSock SubSock;

        private ReceiverBase _callbacks;

        public SmartSock(ReceiverBase callbacks)
        {
            SubSock = new BareSock(this);
            _callbacks = callbacks;
        }

        public void Connect(IPAddress address, int port)
        {
            SubSock.Connect(address, port);
        }

        public void Receive()
        {
            SubSock.Receive();
        }

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            var header = new PacketHeader(buffer, offset);
            if (length == header.Length)
            {
                _callbacks.OnReceive(
                    buffer,
                    offset + PacketHeader.HeaderLength,
                    length - PacketHeader.HeaderLength,
                    endPoint);
            }
            //else // Wrong packet
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            var fullBuffer = Wrap(buffer, offset, length);

            SubSock.Send(endPoint, fullBuffer, 0, fullBuffer.Length);
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            var fullBuffer = Wrap(buffer, offset, length);

            Send(fullBuffer, offset, fullBuffer.Length);
        }

        private static byte[] Wrap(byte[] buffer, int offset, int length)
        {
            var fullBuffer = new byte[length + PacketHeader.HeaderLength];
            var header = new PacketHeader((ushort)fullBuffer.Length);
            // TODO: pool them
            header.WriteTo(fullBuffer, 0);
            // TODO: find more optimal way
            Array.Copy(buffer, offset, fullBuffer, PacketHeader.HeaderLength, length);
            return fullBuffer;
        }
    }
}
