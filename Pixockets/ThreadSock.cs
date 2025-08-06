using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Pixockets.DebugTools;
using Pixockets.Pools;

namespace Pixockets
{
    public class ThreadSock : SockBase
    {
        public Socket SysSock;

        public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }
        public override IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _receiveEndPoint;

        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly BufferPoolBase _buffersPool;
        private readonly ILogger _logger;
        private readonly Thread _sendThread;
        private readonly ThreadSafeQueue<PacketToSend> _sendQueue = new ThreadSafeQueue<PacketToSend>();

        private readonly Thread _receiveThread;
        private readonly ThreadSafeQueue<ReceivedPacket> _recvQueue = new ThreadSafeQueue<ReceivedPacket>();

        private readonly ThreadSafeQueue<SocketException> _excQueue = new ThreadSafeQueue<SocketException>();
        private readonly object _syncObj = new object();
        private bool _connectedMode;

        private const int SendQueueLimit = 10000;
        private const int RecvQueueLimit = 10000;

        public ThreadSock(BufferPoolBase buffersPool, AddressFamily addressFamily, ILogger logger)
        {
            SysSock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

            _buffersPool = buffersPool;
            _logger = logger;
            _sendThread = new Thread(SendLoop);
            _sendThread.IsBackground = true;
            _sendThread.Start();
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
        }

        public override void Connect(IPAddress address, int port)
        {
            if (_cancel.IsCancellationRequested)
                return;

            _remoteEndPoint = new IPEndPoint(address, port);
            AddressFamily addressFamily;
            lock (_syncObj)
            {
                addressFamily = SysSock.AddressFamily;
                if (SysSock.IsBound)
                {
                    SysSock = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);
                }
                SysSock.Connect(_remoteEndPoint);
            }

            _receiveEndPoint = new IPEndPoint(AnyAddress(addressFamily), 0);

            _connectedMode = true;

            if (_receiveThread.ThreadState != ThreadState.Running)
            {
                _receiveThread.Start();
            }
        }

        public override void Listen(int port)
        {
            if (_cancel.IsCancellationRequested)
                return;

            lock (_syncObj)
            {
                _receiveEndPoint = new IPEndPoint(AnyAddress(SysSock.AddressFamily), port);
                _remoteEndPoint = _receiveEndPoint;
                SysSock.Bind(_remoteEndPoint);
            }

            if (_receiveThread.ThreadState != ThreadState.Running)
            {
                _receiveThread.Start();
            }
        }

        public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            Send(RemoteEndPoint, buffer, offset, length, putBufferToPool);
        }

        public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
        {
            if (_excQueue.TryTake(out var e))
            {
                _logger.Exception(e);
                throw e;
            }

            ValidateLength(length);

            var packet = new PacketToSend();
            packet.EndPoint = endPoint;
            packet.Buffer = buffer;
            packet.Offset = offset;
            packet.Length = length;
            packet.PutBufferToPool = putBufferToPool;

            _sendQueue.Add(packet);

            if (_sendQueue.Count > SendQueueLimit)
            {
                // Trash oldest
                if (_sendQueue.TryTake(out packet))
                {
                    if (packet.PutBufferToPool)
                        _buffersPool.Put(packet.Buffer);
                }
            }
        }

        private void SendLoop()
        {
            while (!_cancel.IsCancellationRequested)
            {
                PacketToSend packet = default;
                try
                {
                    packet = _sendQueue.Take(_cancel.Token);
                    if (!_connectedMode)
                        // This is not working for iOS/MacOS after connect call
                        SysSock.SendTo(packet.Buffer, packet.Offset, packet.Length, SocketFlags.None, packet.EndPoint);
                    else
                        SysSock.Send(packet.Buffer, packet.Offset, packet.Length, SocketFlags.None);
                }
                catch (SocketException se)
                {
                    // Ignore harmless errors
                    if (!HarmlessErrors.Contains(se.SocketErrorCode))
                    {
                        _excQueue.Add(se);
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Just quit
                    break;
                }
                finally
                {
                    if (packet.PutBufferToPool)
                        _buffersPool.Put(packet.Buffer);
                }
            }
        }

        private void ReceiveLoop()
        {
            while (!_cancel.IsCancellationRequested)
            {
                var bufferInUse = false;
                byte[] buffer = null;
                EndPoint remoteEP = _receiveEndPoint;
                try
                {
                    if (!SysSock.Poll(10000, SelectMode.SelectRead))
                        continue;

                    buffer = _buffersPool.Get(MTUSafe);
                    var bytesReceived = SysSock.ReceiveFrom(buffer, MTUSafe, SocketFlags.None, ref remoteEP);
                    //ntrf: On windows we will get EMSGSIZE error if message was truncated, but Mono on Unix will fill up the
                    //      whole buffer silently. We detect this case by allowing buffer to be slightly larger, than our typical
                    //      packet, and dropping any packet, that did fill the whole thing.
                    if (bytesReceived > 0 && bytesReceived <= MTU)
                    {
                        var packet = new ReceivedPacket();
                        packet.Buffer = buffer;
                        packet.Offset = 0;
                        packet.Length = bytesReceived;
                        packet.EndPoint = (IPEndPoint)remoteEP;

                        bufferInUse = true;

                        _recvQueue.Add(packet);

                        if (_recvQueue.Count > RecvQueueLimit)
                        {
                            // Trash oldest
                            if (_recvQueue.TryTake(out packet))
                                _buffersPool.Put(packet.Buffer);
                        }
                    }
                    else
                    {
                        // If blocking call isn't actually blocking, sleep for a while
                        Thread.Sleep(10);
                    }
                }
                catch (SocketException se)
                {
                    // Ignore harmless errors
                    if (!HarmlessErrors.Contains(se.SocketErrorCode))
                    {
                        _excQueue.Add(se);
                        break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Just quit
                    break;
                }
                finally
                {
                    if (buffer != null && !bufferInUse)
                        _buffersPool.Put(buffer);
                }
            }
        }

        public override bool Receive(ref ReceivedPacket packet)
        {
            if (_excQueue.TryTake(out var e))
            {
                _logger.Exception(e);
                throw e;
            }

            return _recvQueue.TryTake(out packet);
        }

        public override void Close()
        {
            _cancel.Cancel();
            SysSock?.Close();
            SysSock = null;
            _sendThread.Join();
            if (_receiveThread.IsAlive)
            {
                _receiveThread.Join();
            }
        }
    }
}
