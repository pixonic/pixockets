using System.Collections.Generic;
using System.Threading;

namespace Pixockets
{
    public class ThreadSafeQueue<T>
    {
        private LinkedList<T> _nodes = new LinkedList<T>();
        private AutoResetEvent _added = new AutoResetEvent(false);
        private readonly object _syncRoot = new object();

        public void Add(T item)
        {
            lock (_syncRoot)
            {
                _nodes.AddLast(item);
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
                    if (_nodes.Count > 0)
                    {
                        var val = _nodes.First.Value;
                        _nodes.RemoveFirst();
                        return val;
                    }
                }

                _added.WaitOne();
            }
        }
    }
}
