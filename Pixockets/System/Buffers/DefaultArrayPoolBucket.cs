// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#if DEBUG
using System.Collections.Generic;
#endif
using System.Diagnostics;
using System.Threading;

namespace System.Buffers
{
    internal sealed partial class DefaultArrayPool<T> : ArrayPool<T>
    {
        /// <summary>Provides a thread-safe bucket containing buffers that can be Rent'd and Return'd.</summary>
        private sealed class Bucket
        {
            internal readonly int _bufferLength;
            private readonly T[][] _buffers;
            private readonly int _poolId;

            private readonly object _syncObj = new object();
            private int _index;

#if DEBUG
            private readonly HashSet<T[]> _unique = new HashSet<T[]>();
#endif
            /// <summary>
            /// Creates the pool with numberOfBuffers arrays where each buffer is of bufferLength length.
            /// </summary>
            internal Bucket(int bufferLength, int numberOfBuffers, int poolId)
            {
                _buffers = new T[numberOfBuffers][];
                _bufferLength = bufferLength;
                _poolId = poolId;
            }

            /// <summary>Gets an ID for the bucket to use with events.</summary>
            private int Id { get { return GetHashCode(); } }

            /// <summary>Takes an array from the bucket.  If the bucket is empty, returns null.</summary>
            internal T[] Rent()
            {
                T[][] buffers = _buffers;
                T[] buffer = null;

                // While holding the lock, grab whatever is at the next available index and
                // update the index.  We do as little work as possible while holding the spin
                // lock to minimize contention with other threads.
                bool allocateBuffer = false;

                lock (_syncObj)
                {
                    if (_index < buffers.Length)
                    {
                        buffer = buffers[_index];
                        buffers[_index++] = null;
                        allocateBuffer = buffer == null;
                    }
                }

                // While we were holding the lock, we grabbed whatever was at the next available index, if
                // there was one.  If we tried and if we got back null, that means we hadn't yet allocated
                // for that slot, in which case we should do so now.
                if (allocateBuffer)
                {
                    buffer = new T[_bufferLength];
                }
#if DEBUG
                lock (_unique)
                {
                    _unique.Remove(buffer);
                }
#endif
                return buffer;
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
                    // Just ignore alien buffers
                    //throw new ArgumentException("BufferNotFromPool", nameof(array));
                    return;
                }
#if DEBUG
                lock (_unique)
                {
                    if (_unique.Contains(array))
                    {
                        throw new ArgumentException("Double return of size " + array.Length);
                    }

                    _unique.Add(array);
                }
#endif
                // While holding the spin lock, if there's room available in the bucket,
                // put the buffer into the next available slot.  Otherwise, we just drop it.
                // The try/finally is necessary to properly handle thread aborts on platforms
                // which have them.
                lock (_syncObj)
                {
                    if (_index != 0)
                    {
                        _buffers[--_index] = array;
                    }
                }
            }
        }
    }
}
