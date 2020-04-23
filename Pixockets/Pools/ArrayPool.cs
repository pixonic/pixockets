using System;

namespace Pixockets.Pools
{
    public sealed partial class ArrayPool<T>
    {
        // The default maximum length of each array in the pool
        private const int DefaultMaxArrayLength = 1024 * 64;
        // The default maximum number of arrays per bucket that are available for rent.
        private const int DefaultNumberOfArraysPerBucket = 16;
        // Lazily-allocated empty array used when arrays of length 0 are requested.
        private static T[] _sEmptyArray;

        private readonly Bucket[] _buckets;

        public ArrayPool() : this(DefaultMaxArrayLength, DefaultNumberOfArraysPerBucket)
        {
        }

        public ArrayPool(int maxArrayLength, int arraysPerBucket)
        {
            if (maxArrayLength <= 0)
            {
                throw new ArgumentOutOfRangeException("maxArrayLength");
            }
            if (arraysPerBucket <= 0)
            {
                throw new ArgumentOutOfRangeException("arraysPerBucket");
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

        public T[] Rent(int minimumLength)
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

        public void Return(T[] array)
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

            // Return buffer if we have bucket for it
            if (bucket < _buckets.Length)
            {
                _buckets[bucket].Return(array);
            }
        }
    }
}
