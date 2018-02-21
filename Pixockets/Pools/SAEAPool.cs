using System.Net.Sockets;

namespace Pixockets
{
    // SocketAsyncEventArgsPool
    public class SAEAPool
    {
        private readonly ThreadSafeStack<SocketAsyncEventArgs> _stack = new ThreadSafeStack<SocketAsyncEventArgs>();

        public SocketAsyncEventArgs Get()
        {
            SocketAsyncEventArgs result;
            if (_stack.TryPop(out result))
            {
                return result;
            }

            return null;
        }

        public void Put(SocketAsyncEventArgs obj)
        {
            _stack.Push(obj);
        }
    }
}
