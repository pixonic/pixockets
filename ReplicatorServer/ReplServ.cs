using Pixockets;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;

namespace ReplicatorServer
{
    public class ReplServ : SmartReceiverBase
    {
        private SmartSock _servSock;
        private ConcurrentDictionary<IPEndPoint, ClientState> _clients = new ConcurrentDictionary<IPEndPoint, ClientState>();
        private int _nextCliId = 0;

        public ReplServ()
        {
        }

        public void SetSocket(SmartSock socket)
        {
            _servSock = socket;
        }

        private void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint, bool inOrder)
        {
            if (!inOrder)
            {
                Console.WriteLine("!!! OutOfOrder !!!");
                return;
            }

            ClientState cliVal = null;

            if (!_clients.TryGetValue(endPoint, out cliVal))
            {
            //    Console.WriteLine("Received packet from not connected {0}:{1}", endPoint.Address, endPoint.Port);
            }

            int packetId = buffer[offset];

            if (cliVal == null)
            {
                if (packetId == 0) // Init request
                {
                    cliVal = new ClientState();
                    cliVal.Id = _nextCliId++;
                    cliVal.Red = buffer[offset + 1];
                    cliVal.Green = buffer[offset + 2];
                    cliVal.Blue = buffer[offset + 3];
                    cliVal.X = BitConverter.ToSingle(buffer, offset + 4);
                    cliVal.Y = BitConverter.ToSingle(buffer, offset + 8);
                    Console.WriteLine("Received initial packet");
                    _clients[endPoint] = cliVal;

                    var ms = new MemoryStream();
                    ms.WriteByte(0);  // Init response
                    ms.Write(BitConverter.GetBytes(cliVal.Id), 0, 4);
                    var sendBuffer = ms.ToArray();
                    _servSock.Send(endPoint, sendBuffer, 0, sendBuffer.Length, true);
                }
                else
                {
                    Console.WriteLine("Received packet with wrong id {0} for unknown client", packetId);
                }
            }
            else
            {
                if (packetId == 1)
                {
                    var rX = BitConverter.ToSingle(buffer, offset + 1);
                    var rY = BitConverter.ToSingle(buffer, offset + 5);

                    if (rX != cliVal.X || rY != cliVal.Y)
                    {
                        Console.WriteLine("Received packet with id {0}, x = {1}, y = {2}", packetId, cliVal.X, cliVal.Y);
                    }

                    cliVal.X = rX;
                    cliVal.Y = rY;
                }
                else
                {
                    Console.WriteLine("Received packet with wrong id {0} for known client", packetId);
                }
            }

            //Console.WriteLine("Received: {0}:{1}:{2}", endPoint.Address, endPoint.Port, _clients[endPoint]);
        }

        public void Tick()
        {
            var packet = new ReceivedSmartPacket();
            while (true)
            {
                if (_servSock.Receive(ref packet))
                {
                    OnReceive(packet.Buffer, packet.Offset, packet.Length, packet.EndPoint, packet.InOrder);
                }
                else
                {
                    break;
                }
            }

            _servSock.Tick();

            var ms = new MemoryStream();
            ms.WriteByte(1);  // Tick packet
            ms.Write(BitConverter.GetBytes(_clients.Count), 0, 4);
            foreach (var client in _clients)
            {
                ms.Write(BitConverter.GetBytes(client.Value.Id), 0, 4);
                ms.WriteByte(client.Value.Red);
                ms.WriteByte(client.Value.Green);
                ms.WriteByte(client.Value.Blue);
                ms.Write(BitConverter.GetBytes(client.Value.X), 0, 4);
                ms.Write(BitConverter.GetBytes(client.Value.Y), 0, 4);
            }
            var sendBuffer = ms.ToArray();
            Broadcast(sendBuffer, 0, sendBuffer.Length);
        }

        public void Broadcast(byte[] buffer, int offset, int length)
        {
            foreach (var client in _clients)
            {
                _servSock.Send(client.Key, buffer, offset, length, false);
            }
        }

        public override void OnConnect(IPEndPoint endPoint)
        {
            Console.WriteLine("Connected: {0}:{1}", endPoint.Address, endPoint.Port);
        }

        public override void OnDisconnect(IPEndPoint endPoint, DisconnectReason reason)
        {
            Console.WriteLine("Disconnected: {0}:{1}", endPoint.Address, endPoint.Port);
            ClientState ts;
            _clients.TryRemove(endPoint, out ts);
        }
    }
}
