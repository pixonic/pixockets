using Pixockets;
using System;
using System.Net;

namespace TestClient
{
    public class PrintingReceiver : SmartReceiverBase
    {
        public bool Connected { get; private set; }

        public PrintingReceiver()
        {
            Connected = true;
        }

        public override void OnConnect(IPEndPoint endPoint)
        {
            Connected = true;
            Console.WriteLine("Connected: {0}:{1}", endPoint.Address, endPoint.Port);
        }

        public override void OnDisconnect(IPEndPoint endPoint)
        {
            Connected = false;
            Console.WriteLine("Disconnected: {0}:{1}", endPoint.Address, endPoint.Port);
        }

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint, bool inOrder)
        {
            if (!inOrder)
            {
                Console.WriteLine("!!! OutOfOrder !!!");
                return;
            }

            var count = BitConverter.ToInt32(buffer, offset);
            Console.WriteLine("Count: {0}", count);
            for (int i = 0; i < count; ++i)
            {
                var ts = BitConverter.ToInt32(buffer, offset+i*4+4);
                Console.WriteLine("Timestamp {0}: {1}", i, ts);
            }
        }
    }
}
