using System;

namespace Pixockets.Pools
{
    public class ArrayPoolBucket<T>
    {
        // Provides a thread-safe bucket containing buffers that can be Rented and Returned.
        private readonly int _bufferLength;
        private T[][] _buffers;

        private readonly object _syncObj = new object();
        private int _length;

        // Creates the pool with numberOfBuffers arrays where each buffer is of bufferLength length.
        public ArrayPoolBucket(int bufferLength, int numberOfBuffers)
        {
            _buffers = new T[numberOfBuffers][];
            _bufferLength = bufferLength;
        }

        // Takes an array from the bucket. If the bucket is empty, returns new array.
        public T[] Rent()
        {
            lock (_syncObj)
            {
                if (_length > 0)
                {
                    _length--;
                    var buffer = _buffers[_length];
                    _buffers[_length] = null;
                    return buffer;
                }

                return new T[_bufferLength];
            }
        }

        // Attempts to return the buffer to the bucket.
        public void Return(T[] array)
        {
            // Check to see if the buffer is the correct size for this bucket
            if (array.Length != _bufferLength)
            {
                throw new ArgumentException("BufferNotFromPool", "array");
            }

            lock (_syncObj)
            {
                if (_length == _buffers.Length)
                {
                    var moreBuffers = new T[_length * 2][];
                    Array.Copy(_buffers, moreBuffers, _length);
                    Array.Clear(_buffers, 0, _length);
                    _buffers = moreBuffers;
                }

                _buffers[_length++] = array;
            }
        }
    }
}
