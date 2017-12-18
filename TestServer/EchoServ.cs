using Pixockets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace TestServer
{
    public class EchoServ : ReceiverBase
    {
        private BareSock _servSock;
        private Dictionary<IPEndPoint, long> _clients = new Dictionary<IPEndPoint, long>();
        private Stopwatch _timer = new Stopwatch();

        public EchoServ()
        {
            _timer.Start();
        }

        public void SetSocket(BareSock socket)
        {
            _servSock = socket;
        }

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            if (!_clients.ContainsKey(endPoint))
            {
                _clients.Add(endPoint, _timer.ElapsedTicks);
            }

            var str = Encoding.UTF8.GetString(buffer, offset, length);
            Console.WriteLine("Receive: {0}:{1}:{2}", endPoint.Address, endPoint.Port, str);

            var sendBuffer = Encoding.UTF8.GetBytes("#: " + str);
            Broadcast(sendBuffer, 0, sendBuffer.Length);
        }

        public void Broadcast(byte[] buffer, int offset, int length)
        {
            foreach (var client in _clients)
            {
                _servSock.Send(client.Key, buffer, offset, length);
            }
        }
    }
}
