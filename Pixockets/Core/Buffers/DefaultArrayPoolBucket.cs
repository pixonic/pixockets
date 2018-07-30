// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Pixockets.Core.Buffers
{
    public sealed partial class DefaultArrayPool<T>: ArrayPool<T>
    {
        /// <summary>Provides a thread-safe bucket containing buffers that can be Rent'd and Return'd.</summary>
        private sealed class Bucket
        {
            private readonly int _bufferLength;
            private T[][] _buffers;

            private readonly object _syncObj = new object();
            private int _length;

            /// <summary>
            /// Creates the pool with numberOfBuffers arrays where each buffer is of bufferLength length.
            /// </summary>
            internal Bucket(int bufferLength, int numberOfBuffers)
            {
                _buffers = new T[numberOfBuffers][];
                _bufferLength = bufferLength;
            }

            /// <summary>Takes an array from the bucket.  If the bucket is empty, returns null.</summary>
            internal T[] Rent()
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

            /// <summary>
            /// Attempts to return the buffer to the bucket.  If successful, the buffer will be stored
            /// in the bucket and true will be returned; otherwise, the buffer won't be stored, and false
            /// will be returned.
            /// </summary>
            internal void Return(T[] array)
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
}
