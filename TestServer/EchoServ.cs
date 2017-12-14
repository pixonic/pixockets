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

            foreach (var client in clients)
            {
                Console.WriteLine("Send: {0}:{1}", client.Key.Address, client.Key.Port);
                // Send response
                var sendBuffer = Encoding.UTF8.GetBytes("#: " + str);
                servSock.Send(client.Key, sendBuffer, 0, sendBuffer.Length);
            }
        }
    }
}
