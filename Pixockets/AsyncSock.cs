using System;
using System.Net;
using System.Net.Sockets;
using Pixockets.DebugTools;
using Pixockets.Pools;

namespace Pixockets
{
	public class AsyncSock : SockBase
	{
		public Socket SysSock;

		public override IPEndPoint LocalEndPoint { get { return (IPEndPoint)SysSock.LocalEndPoint; } }

		public override IPEndPoint RemoteEndPoint { get { return _remoteEndPoint; } }

		private IPEndPoint _remoteEndPoint;
		private IPEndPoint _receiveEndPoint;
		private bool _listen = false;

		private readonly BufferPoolBase _buffersPool;
		private readonly ILogger _logger;
		private AddressFamily _addressFamily;

		public AsyncSock(BufferPoolBase buffersPool, AddressFamily addressFamily, ILogger logger)
		{
			_buffersPool = buffersPool;
			_addressFamily = addressFamily;
			_logger = logger;
		}

        public AsyncSock(BufferPoolBase buffersPool, AddressFamily addressFamily)
			: this(buffersPool, addressFamily, new LoggerStub())
		{
		}

		private bool Begin()
		{
			if (SysSock != null)
				return true;

			// ex: propagate
			SysSock = new Socket(_addressFamily, SocketType.Dgram, ProtocolType.Unspecified);
			SysSock.Blocking = false;

			if (_listen)
			{
				SysSock.Bind(_receiveEndPoint);
			}

			return SysSock != null;
		}

		private void End()
		{
			if (SysSock != null)
			{
				SysSock.Close();
				SysSock.Dispose();
			}
			SysSock = null;
		}

		public override void Connect(IPAddress address, int port)
		{
			End();

			_addressFamily = address.AddressFamily;

			_remoteEndPoint = new IPEndPoint(address, port);
			_receiveEndPoint = new IPEndPoint(AnyAddress(_addressFamily), 0);
			_listen = false;

			Begin();
		}

		public override void Listen(int port)
		{
			End();

			_receiveEndPoint = new IPEndPoint(AnyAddress(_addressFamily), port);
			_remoteEndPoint = _receiveEndPoint;
			_listen = true;

			Begin();
		}

		public override void Send(IPEndPoint endPoint, byte[] buffer, int offset, int length, bool putBufferToPool)
		{
			ValidateLength(length);

			try
			{
				// Make sure socket is live
				if (!Begin())
					return;

				SysSock.SendTo(buffer, offset, length, SocketFlags.None, endPoint);
			}
			catch (SocketException sx)
			{
				var err = sx.SocketErrorCode;

				// Ignore harmless errors
				if (!HarmlessErrors.Contains(err))
				{
					_logger.Exception(sx);
					End();

					// Our socket might be killed by iOS
					// Make sure we'll create a new one on our next call
					if (err != SocketError.NotConnected)
					{
						throw sx;
					}
				}
			}
			finally
			{
				if (putBufferToPool)
					_buffersPool.Put(buffer);
			}
		}

		public override void Send(byte[] buffer, int offset, int length, bool putBufferToPool)
		{
			Send(_remoteEndPoint, buffer, offset, length, putBufferToPool);
		}

        public override bool Receive(ref ReceivedPacket packet)
        {
            byte[] buffer = null;
            var bufferInUse = false;
            try
            {
				if (!Begin())
					return false;

                if (!SysSock.Poll(0, SelectMode.SelectRead))
                    return false;

				buffer = _buffersPool.Get(MTUSafe);
                EndPoint remoteEP = _receiveEndPoint;

                var bytesReceived = SysSock.ReceiveFrom(buffer, MTUSafe, SocketFlags.None, ref remoteEP);
                //ntrf: On windows we will get EMSGSIZE error if message was truncated, but Mono on Unix will fill up the
                //      whole buffer silently. We detect this case by allowing buffer to be slightly larger, than our typical
                //      packet, and dropping any packet, that did fill the whole thing.
                if (bytesReceived > 0 && bytesReceived <= MTU)
                {
                    packet.Buffer = buffer;
                    bufferInUse = true;
                    packet.Offset = 0;
                    packet.Length = bytesReceived;
                    packet.EndPoint = (IPEndPoint)remoteEP;

                    return true;
                }
            }
            catch (SocketException sx)
            {
                var err = sx.SocketErrorCode;

				// Ignore harmless errors
				// On Windows machines we might have a case of ICMP Port Unreachable being delivered,
				// which causes ECONNRESET on next receive call
				if (!HarmlessErrors.Contains(err))
				{
					End();

					// Our socket might be killed by iOS. Recreate the socket.
					if (err != SocketError.NotConnected)
					{
						_logger.Exception(sx);
						throw;
					}
				}
            }
            finally
            {
	            // We don't return buffer here if it is to be processed by client
	            if (buffer != null && !bufferInUse)
		            _buffersPool.Put(buffer);
            }

            return false;
        }

        public override void Close()
        {
			End();
            _remoteEndPoint = null;
        }
    }
}
