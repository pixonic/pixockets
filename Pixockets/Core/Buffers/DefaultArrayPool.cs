// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Core.Buffers;

namespace Pixockets.Core.Buffers
{
    public sealed partial class DefaultArrayPool<T> : ArrayPool<T>
    {
        /// <summary>The default maximum length of each array in the pool (2^16).</summary>
        private const int DefaultMaxArrayLength = 1024 * 64;  // 1024 * 1024 (2^20);
        /// <summary>The default maximum number of arrays per bucket that are available for rent.</summary>
        private const int DefaultNumberOfArraysPerBucket = 16;
        /// <summary>Lazily-allocated empty array used when arrays of length 0 are requested.</summary>
        private static T[] _sEmptyArray; // we support contracts earlier than those with Array.Empty<T>()

        private readonly Bucket[] _buckets;

        public DefaultArrayPool() : this(DefaultMaxArrayLength, DefaultNumberOfArraysPerBucket)
        {
        }

        public DefaultArrayPool(int maxArrayLength, int arraysPerBucket)
        {
            if (maxArrayLength <= 0)
            {
                throw new ArgumentOutOfRangeException("maxArrayLength");
            }
            if (arraysPerBucket <= 0)
            {
                throw new ArgumentOutOfRangeException("maxArraysPerBucket");
            }

            // Our bucketing algorithm has a min length of 2^4 and a max length of 2^30.
            // Constrain the actual max used to those values.
            const int minimumArrayLength = 0x10, maximumArrayLength = 0x40000000;
            if (maxArrayLength > maximumArrayLength)
            {
                maxArrayLength = maximumArrayLength;
            }
            else if (maxArrayLength < minimumArrayLength)
            {
                maxArrayLength = minimumArrayLength;
            }

            // Create the buckets.
            int maxBuckets = Utilities.SelectBucketIndex(maxArrayLength);
            var buckets = new Bucket[maxBuckets + 1];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new Bucket(Utilities.GetMaxSizeForBucket(i), arraysPerBucket);
            }
            _buckets = buckets;
        }

        public override T[] Rent(int minimumLength)
        {
            // Arrays can't be smaller than zero.  We allow requesting zero-length arrays (even though
            // pooling such an array isn't valuable) as it's a valid length array, and we want the pool
            // to be usable in general instead of using `new`, even for computed lengths.
            if (minimumLength < 0)
            {
                throw new ArgumentOutOfRangeException("minimumLength");
            }
            else if (minimumLength == 0)
            {
                // No need for events with the empty array.  Our pool is effectively infinite
                // and we'll never allocate for rents and never store for returns.
                return _sEmptyArray ?? (_sEmptyArray = new T[0]);
            }

            int index = Utilities.SelectBucketIndex(minimumLength);
            if (index < _buckets.Length)
            {
                return _buckets[index].Rent();
            }

            // The request was for a size too large for the pool.  Allocate an array of exactly the requested length.
            // When it's returned to the pool, we'll simply throw it away.
            return new T[minimumLength];
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            else if (array.Length == 0)
            {
                // Ignore empty arrays.  When a zero-length array is rented, we return a singleton
                // rather than actually taking a buffer out of the lowest bucket.
                return;
            }

            // Determine with what bucket this array length is associated
            int bucket = Utilities.SelectBucketIndex(array.Length);

            // If we can tell that the buffer was allocated, drop it. Otherwise, check if we have space in the pool
            if (bucket < _buckets.Length)
            {
                // Clear the array if the user requests
                if (clearArray)
                {
                    Array.Clear(array, 0, array.Length);
                }

                // Return the buffer to its bucket.  In the future, we might consider having Return return false
                // instead of dropping a bucket, in which case we could try to return to a lower-sized bucket,
                // just as how in Rent we allow renting from a higher-sized bucket.
                _buckets[bucket].Return(array);
            }
        }
    }
}
