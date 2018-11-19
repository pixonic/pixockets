using System.Threading;

namespace Pixockets
{
    // TODO: split to blocking and non-blocking?
    public class ThreadSafeQueue<T>
    {
        private class ArrayNode : IPoolable
        {
            public const int MaxCount = 32;

            public ArrayNode Next;
            public readonly T[] Items = new T[MaxCount];
            public int Lo;
            public int Hi;

            public void Strip()
            {
                Lo = Hi = 0;
                Next = null;
            }
        }

        public const int SizeLimit = 50000;

        private readonly Pool<ArrayNode> _nodesPool = new Pool<ArrayNode>();
        private readonly AutoResetEvent _added = new AutoResetEvent(false);
        private readonly object _syncRoot = new object();
        private ArrayNode _head;
        private ArrayNode _tail;
        private volatile int _count;

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _count;
                }
            }
        }

        public ThreadSafeQueue()
        {
            _tail = _head = _nodesPool.Get();
        }

        public void Add(T item)
        {
            lock (_syncRoot)
            {
                AddLast(item);
                if (_count > SizeLimit)
                {
                    var trashIt = RemoveFirst();
                }
            }
            _added.Set();
        }

        // Blocking
        public T Take()
        {
            while (true)
            {
                lock (_syncRoot)
                {
                    if (_count > 0)
                    {
                        var val = RemoveFirst();
                        return val;
                    }
                }

                _added.WaitOne();
            }
        }

        public bool TryTake(out T item)
        {
            if (_count == 0)
            {
                item = default(T);
                return false;
            }

            lock (_syncRoot)
            {
                if (_count > 0)
                {
                    var val = RemoveFirst();
                    item = val;
                    return true;
                }
            }

            item = default(T);
            return false;
        }

        private void AddLast(T item)
        {
            if (_tail.Hi >= ArrayNode.MaxCount)
            {
                var node = _nodesPool.Get();
                _tail.Next = node;
                _tail = node;
            }

            _tail.Items[_tail.Hi++] = item;
            _count++;
        }

        private T RemoveFirst()
        {
            var result = _head.Items[_head.Lo];
            _head.Items[_head.Lo++] = default(T);
            _count--;

            if (_head.Lo >= _head.Hi)
            {
                var node = _head;
                _head = _head.Next;
                _nodesPool.Put(node);
                if (_head == null)
                {
                    _head = _tail = _nodesPool.Get();
                }
            }
            return result;
        }
    }
}
