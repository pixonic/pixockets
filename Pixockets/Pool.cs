using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixockets
{
    public class Pool<T> where T: class, IPoolable, new()
    {
        private ConcurrentStack<T> _stack = new ConcurrentStack<T>();
        public T Get()
        {
            T result;
            if (_stack.TryPop(out result))
            {
                return result;
            }

            return new T();
        }

        public void Put(T obj)
        {
            obj.Strip();
            _stack.Push(obj);
        }
    }
}
