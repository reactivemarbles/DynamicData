using System.Collections.Generic;

namespace DynamicData.List.Internal
{
    /// <summary>
    /// Ripped and adapted from https://clinq.codeplex.com/
    /// 
    /// Thanks dudes
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ReferenceCountTracker<T>
    {
        private Dictionary<T, int> ReferenceCounts { get; } = new Dictionary<T, int>();

        public IEnumerable<T> Items => ReferenceCounts.Keys;

        public int this[T item] => ReferenceCounts[item];

        /// <summary>
        ///     Increments the reference count for the item.  Returns true when refrence count goes from 0 to 1.
        /// </summary>
        public bool Add(T item)
        {
            int currentCount;
            if (!ReferenceCounts.TryGetValue(item, out currentCount))
            {
                ReferenceCounts.Add(item, 1);
                return true;
            }

            ReferenceCounts[item] = currentCount + 1;
            return false;
        }

        public void Clear()
        {
            ReferenceCounts.Clear();
        }

        /// <summary>
        ///     Decrements the reference count for the item.  Returns true when refrence count goes from 1 to 0.
        /// </summary>
        public bool Remove(T item)
        {
            int currentCount = ReferenceCounts[item];

            if (currentCount == 1)
            {
                ReferenceCounts.Remove(item);
                return true;
            }

            ReferenceCounts[item] = currentCount - 1;
            return false;
        }

        public bool Contains(T item)
        {
            return ReferenceCounts.ContainsKey(item);
        }
    }
}
