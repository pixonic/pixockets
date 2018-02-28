using System.Collections.Generic;

namespace Pixockets
{
    public class FlatStack<T>
    {
        private List<T> _items = new List<T>();

        public bool TryPop(out T result)
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

        public void Push(T item)
        {
            _items.Add(item);
        }
    }
}
