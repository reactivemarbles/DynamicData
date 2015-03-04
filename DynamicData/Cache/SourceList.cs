using System;

namespace DynamicData
{
    /// <summary>
    /// A source list which uses the hash code to uniquely identify items
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SourceCache<T> : SourceCache<T, int>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SourceCache{T}"/> class.
        /// </summary>
        public SourceCache()
            : base(t => t.GetHashCode())
        {
        }
    }
}