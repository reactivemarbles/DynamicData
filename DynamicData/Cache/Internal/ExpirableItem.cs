using System;
using System.Collections.Generic;

namespace DynamicData.Internal
{
    internal sealed class ExpirableItem<TObject, TKey> : IEquatable<ExpirableItem<TObject, TKey>>
    {
        private readonly TKey _key;
        private readonly TObject _value;
        private readonly DateTime _expireAt;
        private readonly long _index;

        public ExpirableItem(TObject value, TKey key, DateTime dateTime, long index = 0)
        {
            _value = value;
            _key = key;
            _expireAt = dateTime;
            _index = index;
        }

        public TObject Value { get { return _value; } }

        public TKey Key { get { return _key; } }

        public DateTime ExpireAt { get { return _expireAt; } }

        public long Index { get { return _index; } }

        #region Equality members

        public bool Equals(ExpirableItem<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(_key, other._key) && _expireAt.Equals(other._expireAt);
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
                return (EqualityComparer<TKey>.Default.GetHashCode(_key) * 397) ^ _expireAt.GetHashCode();
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
            return string.Format("Key: {0}, Expire At: {1}", _key, _expireAt);
        }
    }
}
