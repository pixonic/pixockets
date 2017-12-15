using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TestWithUdpClient
{
    class Program
    {
        static void Main(string[] args)
        {
            UdpClient client = new UdpClient();
            client.Connect(new IPEndPoint(IPAddress.Loopback, 2345));

            client.BeginReceive(new AsyncCallback(Read_Callback), client);

            while (true)
            {
                var msg = Console.ReadLine();

                var buffer = Encoding.UTF8.GetBytes(msg);
                client.Send(buffer, buffer.Length);
            }
        }

        public static void Read_Callback(IAsyncResult ar)
        {
            UdpClient client = (UdpClient)ar.AsyncState;
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            var buff = client.EndReceive(ar, ref remoteEP);

            Console.WriteLine(Encoding.UTF8.GetString(buff));

            client.BeginReceive(new AsyncCallback(Read_Callback), client);
        }
    }
}
