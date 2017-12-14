using Pixockets;
using System;
using System.Net;
using System.Text;

namespace TestClient
{
    public class PrintingReceiver : ReceiverBase
    {
        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint)
        {
            var str = Encoding.UTF8.GetString(buffer, offset, length);
            Console.WriteLine(str);
        }
    }
}
