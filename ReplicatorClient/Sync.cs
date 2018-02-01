using Pixockets;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Media;
using System.Net;
using System.Buffers;
using System.Threading;
using System.IO;

namespace ReplicatorClient
{
    public delegate void FollowerCollectionDelegate(int id);

    public class Sync : SmartReceiverBase
    {
        public FollowerCollectionDelegate OnDeleteFollower;
        public FollowerCollectionDelegate OnNewFollower;

        public int MyId { get { return _myId; } }

        //private ChangesRegistry Changes = new ChangesRegistry();
        //private Networker Connection;
        private MultiDict<int, INotifyPropertyChanged> Followers = new MultiDict<int, INotifyPropertyChanged>();
        private SmartSock _socket;
        private Thread _ticker;
        private Random _rnd = new Random(Guid.NewGuid().GetHashCode());
        private int _myId;
        private Vertex _myV;

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
                var ms = new MemoryStream();
                ms.WriteByte(1); // Move request
                ms.Write(BitConverter.GetBytes(_myV.X), 0, 4);
                ms.Write(BitConverter.GetBytes(_myV.Y), 0, 4);
                var buf = ms.ToArray();
                _socket.Send(buf, 0, buf.Length);

                _socket.Tick();
                Thread.Sleep(100);
            }
        }

        public void Connect(float x, float y)
        {
            _socket = new SmartSock(ArrayPool<byte>.Shared, new BareSock(ArrayPool<byte>.Shared), this);
            // Todo: pass address from command line
            _socket.Connect(IPAddress.Loopback, 2345);
            _socket.Receive();

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


            OnNewFollower(_myId);
            _myV = Followers.Get(_myId) as Vertex;
            _myV.X = x;
            _myV.Y = y;
            _myV.C = brush;

            _socket.SendReliable(sendBuffer, 0, sendBuffer.Length);
        }

        public void AddObject(int id, INotifyPropertyChanged follower)
        {
            if (follower == null)
            {
                return;
            }

            Followers.Add(id, follower);
            //follower.PropertyChanged += FollowerPropertyChanged;
        }
        /*
        private void FollowerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyInfo propInfo = sender.GetType().GetProperty(e.PropertyName);
            string propValue = propInfo.GetValue(sender).ToString();

            Vertex follower = sender as Vertex;
            if (follower != _myV)
                return;

            if (!Followers.ContainsKey(follower))
            {
                return;
            }

            int itemId = Followers.Get(follower);

            try
            {
                //Unsubscribe because we changing it by ourselves
                follower.PropertyChanged -= FollowerPropertyChanged;

                // TODO: refactor
                if (e.PropertyName == "X")
                {
                    _myV.X = float.Parse(propValue);
                }
                else if (e.PropertyName == "Y")
                {
                    _myV.Y = float.Parse(propValue);
                }
            }
            catch (Exception)
            {
                return;
            }
            finally
            {
                //Subscribe to catch changes from other actions
                follower.PropertyChanged += FollowerPropertyChanged;
            }

            //Connection.Send(itemId, e.PropertyName, propValue);
        }
        */
        private void ApplyChanges(int from, int itemId, string propName, string propValue)
        {
            if (propName == "-" && propValue == "-")
            {
                Followers.Remove(itemId);

                OnDeleteFollower(itemId);

                return;
            }

            if (!Followers.ContainsKey(itemId))
            {
                if (OnNewFollower != null)
                {
                    OnNewFollower(itemId);
                }
                else
                {
                    return;
                }
            }

            INotifyPropertyChanged follower = Followers.Get(itemId);

            try
            {
                //Unsubscribe because we changing it by ourselves
                //follower.PropertyChanged -= FollowerPropertyChanged;

                PropertyInfo propertyInfo = follower.GetType().GetProperty(propName);
                if (propertyInfo.PropertyType.FullName == "System.Windows.Media.Brush")
                {
                    Brush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(propValue));
                    brush.Freeze();
                    propertyInfo.SetValue(follower, brush, null);
                }
                else
                {
                    propertyInfo.SetValue(follower, Convert.ChangeType(propValue, propertyInfo.PropertyType), null);
                }
            }
            catch (Exception)
            {
                return;
            }
            finally
            {
                //Subscribe to catch changes from other actions
                //follower.PropertyChanged += FollowerPropertyChanged;
            }
        }


        public void RemoveObject(int id)
        {
            Followers.Remove(id);

            //Connection.Send(id, "-", "-");
        }

        public override void OnReceive(byte[] buffer, int offset, int length, IPEndPoint endPoint, bool inOrder)
        {
            byte packetId = buffer[offset];
            // Init response
            if (packetId == 0)
            {
                _myId = BitConverter.ToInt32(buffer, offset + 1);

                if (OnNewFollower != null)
                {
                    OnNewFollower(_myId);
                    Vertex follower = Followers.Get(_myId) as Vertex;

                    try
                    {
                        //Unsubscribe because we changing it by ourselves
                        //follower.PropertyChanged -= FollowerPropertyChanged;

                        //follower.X = _myX;
                        //follower.Y = _myY;

                        //PropertyInfo xPropInfo = follower.GetType().GetProperty("X");
                        //xPropInfo.SetValue(follower, _myX, null);

                        //PropertyInfo yPropInfo = follower.GetType().GetProperty("Y");
                        //yPropInfo.SetValue(follower, _myY, null);

                        /*
                                                if (propertyInfo.PropertyType.FullName == "System.Windows.Media.Brush")
                                                {
                                                    Brush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(propValue));
                                                    brush.Freeze();
                                                    propertyInfo.SetValue(follower, brush, null);
                                                }
                                                else
                                                {
                                                }*/
                    }
                    catch (Exception)
                    {
                        return;
                    }
                    finally
                    {
                        //Subscribe to catch changes from other actions
                        //follower.PropertyChanged += FollowerPropertyChanged;
                    }

                }
            }
            // Tick response
            else if (packetId == 1)
            {

            }
            else
            {
                // Wrong packet
            }
        }

        public override void OnConnect(IPEndPoint endPoint)
        {
            
        }

        public override void OnDisconnect(IPEndPoint endPoint)
        {
            
        }
    }
}
