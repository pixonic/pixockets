using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Pixockets
{
    public class SmartSock : ReceiverBase
    {
        public readonly BareSock SubSock;

        private ReceiverBase _callbacks;

        public SmartSock(ReceiverBase callbacks)
        {
            SubSock = new BareSock(this);
            _callbacks = callbacks;
        }

        public void Connect(int port)
        {
            SubSock.Connect(port);
        }

        public void ReceiveFrom()
        {
            SubSock.ReceiveFrom();
        }

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            
        }
    }
}
