using System.Net;

namespace Pixockets
{
    public class ThreadSafeSmartSock
    {
        private readonly SmartSock _socket;
        private readonly object _syncObject = new object();

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

        public void Listen(int port)
        {
            lock (_syncObject)
            {
                _socket.Listen(port);
            }
        }

        public bool Receive(ref ReceivedSmartPacket receivedPacket)
        {
            lock (_syncObject)
            {
                return _socket.Receive(ref receivedPacket);
            }
        }

        public void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool reliable)
        {
            lock (_syncObject)
            {
                _socket.Send(endPoint, buffer, offset, length, reliable);
            }
        }
        public void Send(byte[] buffer, int offset, int length, bool reliable)
        {
            lock (_syncObject)
            {
                _socket.Send(buffer, offset, length, reliable);
            }
        }

        public void Tick()
        {
            lock (_syncObject)
            {
                _socket.Tick();
            }
        }

        public void Close()
        {
            lock (_syncObject)
            {
                _socket.Close();
            }
        }
    }
}
