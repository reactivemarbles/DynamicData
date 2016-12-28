using System;
using System.Collections.Generic;

namespace DynamicData.List.Internal
{
    internal sealed class ExpirableItem<TObject> : IEquatable<ExpirableItem<TObject>>
    {
        public TObject Item { get; }
        public DateTime ExpireAt { get; }
        public long Index { get; }

        public ExpirableItem(TObject value, DateTime dateTime, long index)
        {
            Item = value;
            ExpireAt = dateTime;
            Index = index;
        }

        #region Equality members

        public bool Equals(ExpirableItem<TObject> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TObject>.Default.Equals(Item, other.Item) && ExpireAt.Equals(other.ExpireAt) && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ExpirableItem<TObject> && Equals((ExpirableItem<TObject>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = EqualityComparer<TObject>.Default.GetHashCode(Item);
                hashCode = (hashCode * 397) ^ ExpireAt.GetHashCode();
                hashCode = (hashCode * 397) ^ Index.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ExpirableItem<TObject> left, ExpirableItem<TObject> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ExpirableItem<TObject> left, ExpirableItem<TObject> right)
        {
            return !Equals(left, right);
        }

        #endregion

        public override string ToString()
        {
            return $"{Item} @ {ExpireAt}";
        }
    }
}
