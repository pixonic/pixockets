using System.Collections.Generic;

namespace ReplicatorClient
{
    public class MultiDict<K1, K2>
    {
        public Dictionary<K1, K2>.KeyCollection Keys1
        {
            get { return Key1ToKey2.Keys; }            
        }

        public void Add(K1 key1, K2 key2)
        {
            lock (Locker)
            {
                Key1ToKey2.Add(key1, key2);
                Key2ToKey1.Add(key2, key1);
            }
        }

        public K2 Get(K1 key1)
        {
            lock (Locker)
            {
                K2 key2;
                if (Key1ToKey2.TryGetValue(key1, out key2))
                {
                    return key2;
                }
                else
                {
                    return default(K2);
                }
            }
        }

        public K1 Get(K2 key2)
        {
            lock (Locker)
            {
                K1 key1;
                if (Key2ToKey1.TryGetValue(key2, out key1))
                {
                    return key1;
                }
                else
                {
                    return default(K1);
                }
            }
        }

        public bool ContainsKey(K1 key1)
        {
            lock (Locker)
            {
                return Key1ToKey2.ContainsKey(key1);
            }
        }

        public bool ContainsKey(K2 key2)
        {
            lock (Locker)
            {
                return Key2ToKey1.ContainsKey(key2);
            }
        }

        public bool Remove(K1 key1)
        {
            lock (Locker)
            {
                K2 key2;
                if (Key1ToKey2.TryGetValue(key1, out key2))
                {
                    Key1ToKey2.Remove(key1);
                    Key2ToKey1.Remove(key2);

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool Remove(K2 key2)
        {
            lock (Locker)
            {
                K1 key1;
                if (Key2ToKey1.TryGetValue(key2, out key1))
                {
                    Key2ToKey1.Remove(key2);
                    Key1ToKey2.Remove(key1);

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private Dictionary<K1, K2> Key1ToKey2 = new Dictionary<K1, K2>();
        private Dictionary<K2, K1> Key2ToKey1 = new Dictionary<K2, K1>();
        private readonly object Locker = new object();
    }
}
