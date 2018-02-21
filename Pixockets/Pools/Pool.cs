
namespace Pixockets
{
    public class Pool<T> where T: class, IPoolable, new()
    {
        private readonly ThreadSafeStack<T> _stack = new ThreadSafeStack<T>();

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
