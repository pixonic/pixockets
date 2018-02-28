using System.Collections.Generic;

namespace Pixockets
{
    public class ThreadSafeStack<T>
    {
        private List<T> _items = new List<T>();
        private readonly object _syncRoot = new object();

        public bool TryPop(out T result)
        {
            lock (_syncRoot)
            {
                if (_items.Count > 0)
                {
                    result = _items[_items.Count - 1];
                    _items.RemoveAt(_items.Count - 1);
                    return true;
                }
                else
                {
                    result = default(T);
                    return false;
                }
            }
        }

        public void Push(T item)
        {
            lock (_syncRoot)
            {
                _items.Add(item);
            }
        }
    }
}
