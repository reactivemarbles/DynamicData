using System;
using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal sealed class ExpirableItem<TObject, TKey> : IEquatable<ExpirableItem<TObject, TKey>>
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

        public bool Equals(ExpirableItem<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) && ExpireAt.Equals(other.ExpireAt);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ExpirableItem<TObject, TKey>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<TKey>.Default.GetHashCode(Key) * 397) ^ ExpireAt.GetHashCode();
            }
        }

        public static bool operator ==(ExpirableItem<TObject, TKey> left, ExpirableItem<TObject, TKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ExpirableItem<TObject, TKey> left, ExpirableItem<TObject, TKey> right)
        {
            return !Equals(left, right);
        }

        #endregion

        public override string ToString()
        {
            return $"Key: {Key}, Expire At: {ExpireAt}";
        }
    }
}
