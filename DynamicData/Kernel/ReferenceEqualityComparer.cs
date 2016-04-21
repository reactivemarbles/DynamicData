using System.Collections.Generic;

namespace DynamicData
{
    internal class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    {
        public readonly static IEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }
    }
}
