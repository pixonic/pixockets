using Pixockets;
using System;
using System.Windows.Media;
using System.Net;
using System.Threading;
using System.IO;
using System.Collections.Generic;

namespace ReplicatorClient
{
    public delegate void FollowerCollectionDelegate(int id);

    public class Sync : SmartReceiverBase
    {
        public FollowerCollectionDelegate OnDeleteFollower;
        public FollowerCollectionDelegate OnNewFollower;

        public int MyId { get { return _myId; } }

        private volatile bool _initReceived;

        private MultiDict<int, Vertex> Followers = new MultiDict<int, Vertex>();
        private ThreadSafeSmartSock _socket;
        private Thread _ticker;
        private Random _rnd = new Random(Guid.NewGuid().GetHashCode());
        private int _myId;
        private Vertex _myV;
        private HashSet<int> _idsReceived = new HashSet<int>();
        private List<int> _idsToDelete = new List<int>();
        private CoreBufferPool _bufferPool = new CoreBufferPool();

        public void Start(float x, float y)
        {
            Connect(x, y);

            _ticker = new Thread(new ThreadStart(Tick));
            _ticker.IsBackground = true;
            _ticker.Start();
        }

        private void Tick()
        {
            while (true)
            {
                if (_initReceived)
                {
                    var ms = new MemoryStream();
                    ms.WriteByte(1); // Move request

                    ms.Write(BitConverter.GetBytes((float)_myV.X), 0, 4);
                    ms.Write(BitConverter.GetBytes((float)_myV.Y), 0, 4);

                    var buf = ms.ToArray();

                    _socket.Send(buf, 0, buf.Length);
                }

                _socket.Tick();
                var packet = new ReceivedSmartPacket();
                while (true)
                {
                    if (_socket.ReceiveFrom(ref packet))
                    {
                        OnReceive(packet.Buffer, packet.Offset, packet.Length, packet.EndPoint, packet.InOrder);
                        _bufferPool.Put(packet.Buffer);
                    }
                    else
                    {
                        break;
                    }
                }
                Thread.Sleep(100);
            }
        }

        public void Connect(float x, float y)
        {
            var smartSock = new SmartSock(_bufferPool, new ThreadSock(_bufferPool), this);
            _socket = new ThreadSafeSmartSock(smartSock);
            // Todo: pass address from command line
            _socket.Connect(IPAddress.Loopback, 2345);

            var ms = new MemoryStream();
            ms.WriteByte(0);  // Init request packet

            byte red = (byte)_rnd.Next(0, byte.MaxValue + 1);
            byte green = (byte)_rnd.Next(0, byte.MaxValue + 1);
            byte blue = (byte)_rnd.Next(0, byte.MaxValue + 1);
            Brush brush = new SolidColorBrush(Color.FromArgb(255, red, green, blue));

            ms.WriteByte(red);
            ms.WriteByte(green);
            ms.WriteByte(blue);

            ms.Write(BitConverter.GetBytes(x), 0, 4);
            ms.Write(BitConverter.GetBytes(y), 0, 4);

            var sendBuffer = ms.ToArray();

            _myV = new Vertex();
            _myV.X = x;
            _myV.Y = y;
            _myV.C = brush;

            _socket.SendReliable(sendBuffer, 0, sendBuffer.Length);
        }

        public void AddObject(int id, Vertex follower)
        {
            if (follower == null)
            {
                return;
            }

            Followers.Add(id, follower);
        }

        public void RemoveObject(int id)
        {
            Followers.Remove(id);
        }

        private void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint, bool inOrder)
        {
            byte packetId = buffer[offset];
            // Init response
            if (packetId == 0)
            {
                OnInitResponse(buffer, offset);
            }
            // Tick response
            else if (packetId == 1)
            {
                OnTickResponse(buffer, offset);
            }
            else
            {
                // Wrong packet
                Console.WriteLine("Wrong packet with id {0}", packetId);
            }
        }

        private void OnInitResponse(byte[] buffer, int offset)
        {
            _myId = BitConverter.ToInt32(buffer, offset + 1);

            var follower = Followers.Get(_myId) as Vertex;

            if (follower == null)
            {
                OnNewFollower(_myId);
                follower = Followers.Get(_myId) as Vertex;
            }

            follower.X = _myV.X;
            follower.Y = _myV.Y;
            follower.C = _myV.C;

            _myV = follower;

            _initReceived = true;
        }

        private void OnTickResponse(byte[] buffer, int offset)
        {
            if (!_initReceived)
                return;

            var count = BitConverter.ToInt32(buffer, offset + 1);
            for (int i = 0; i < count; ++i)
            {
                var id = BitConverter.ToInt32(buffer, offset + 5 + i * 15);
                _idsReceived.Add(id);
                if (id == _myId)
                    continue;

                byte red = buffer[offset + 9 + i * 15];
                byte green = buffer[offset + 10 + i * 15];
                byte blue = buffer[offset + 11 + i * 15];
                float x = BitConverter.ToSingle(buffer, offset + 12 + i * 15);
                float y = BitConverter.ToSingle(buffer, offset + 16 + i * 15);

                Vertex follower = Followers.Get(id) as Vertex;
                if (follower == null)
                {
                    OnNewFollower(id);
                    follower = Followers.Get(id) as Vertex;
                    var brush = new SolidColorBrush(Color.FromArgb(255, red, green, blue));
                    brush.Freeze();
                    follower.C = brush;
                }

                follower.X = x;
                follower.Y = y;
            }

            foreach (var id in Followers.Keys1)
            {
                if (!_idsReceived.Contains(id))
                {
                    _idsToDelete.Add(id);
                }
            }

            _idsReceived.Clear();

            foreach (var id in _idsToDelete)
            {
                Followers.Remove(id);
                OnDeleteFollower(id);
            }

            _idsToDelete.Clear();
        }

        public override void OnConnect(IPEndPoint endPoint)
        {
            Console.WriteLine("Connect");
        }

        public override void OnDisconnect(IPEndPoint endPoint)
        {
            Console.WriteLine("Disconnect");
        }
    }
}
