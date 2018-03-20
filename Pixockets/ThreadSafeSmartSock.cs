using System.Net;

namespace Pixockets
{
    public class ThreadSafeSmartSock
    {
        private SmartSock _socket;
        private object _syncObject = new object();
        
        public ThreadSafeSmartSock(SmartSock socket)
        {
            _socket = socket;
        }

        public void Connect(IPAddress address, int port)
        {
            lock (_syncObject)
            {
                _socket.Connect(address, port);
            }
        }

        public void Receive(int port)
        {
            lock (_syncObject)
            {
                _socket.Listen(port);
            }
        }

        public bool ReceiveFrom(ref ReceivedSmartPacket receivedPacket)
        {
            lock (_syncObject)
            {
                return _socket.ReceiveFrom(ref receivedPacket);
            }
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            lock (_syncObject)
            {
                _socket.Send(endPoint, buffer, offset, length);
            }
        }

        public void SendReliable(IPEndPoint endPoint, byte[] buffer, int offset, int length)
        {
            lock (_syncObject)
            {
                _socket.SendReliable(endPoint, buffer, offset, length);
            }
        }

        public void Send(byte[] buffer, int offset, int length)
        {
            lock (_syncObject)
            {
                _socket.Send(buffer, offset, length);
            }
        }

        public void SendReliable(byte[] buffer, int offset, int length)
        {
            lock (_syncObject)
            {
                _socket.SendReliable(buffer, offset, length);
            }
        }

        public void Tick()
        {
            lock (_syncObject)
            {
                _socket.Tick();
            }
        }
    }
}
