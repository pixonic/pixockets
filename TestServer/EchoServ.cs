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
        BareSock servSock;
        Dictionary<IPEndPoint, long> clients = new Dictionary<IPEndPoint, long>();
        Stopwatch timer = new Stopwatch();

        public EchoServ()
        {
            timer.Start();
        }

        public void SetSocket(BareSock socket)
        {
            servSock = socket;
        }

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            if (!clients.ContainsKey(endPoint))
            {
                clients.Add(endPoint, timer.ElapsedTicks);
            }

            var str = Encoding.UTF8.GetString(buffer, offset, length);
            Console.WriteLine("Receive: {0}:{1}:{2}", endPoint.Address, endPoint.Port, str);

            var sendBuffer = Encoding.UTF8.GetBytes("#: " + str);
            Broadcast(sendBuffer, 0, sendBuffer.Length);
        }

        public void Broadcast(byte[] buffer, int offset, int length)
        {
            foreach (var client in clients)
            {
                servSock.Send(client.Key, buffer, offset, length);
            }
        }
    }
}
