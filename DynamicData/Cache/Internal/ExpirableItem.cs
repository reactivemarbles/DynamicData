using System;
using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal readonly struct ExpirableItem<TObject, TKey> : IEquatable<ExpirableItem<TObject, TKey>>
    {
        public TObject Value { get; }

        public TKey Key { get; }

        public DateTime ExpireAt { get; }

        public long Index { get; }

        public ExpirableItem(TObject value, TKey key, DateTime dateTime, long index = 0)
        {
            Value = value;
            Key = key;
            ExpireAt = dateTime;
            Index = index;
        }

        #region Equality members

        /// <inheritdoc />
        public bool Equals(ExpirableItem<TObject, TKey> other)
        {
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) && ExpireAt.Equals(other.ExpireAt);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ExpirableItem<TObject, TKey> && Equals((ExpirableItem<TObject, TKey>) obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<TKey>.Default.GetHashCode(Key) * 397) ^ ExpireAt.GetHashCode();
            }
        }

        public static bool operator ==(ExpirableItem<TObject, TKey> left, ExpirableItem<TObject, TKey> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ExpirableItem<TObject, TKey> left, ExpirableItem<TObject, TKey> right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return $"Key: {Key}, Expire At: {ExpireAt}";
        }
    }
}
