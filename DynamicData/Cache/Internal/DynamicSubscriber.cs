using System;
using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal sealed class SubscriptionContainer<TObject, TKey> : IDisposable, IEquatable<SubscriptionContainer<TObject, TKey>>
    {
        private readonly IDisposable _cleanUp;
        private readonly TObject _item;
        private readonly TKey _key;

        public SubscriptionContainer(TObject item, TKey key, Func<TObject, TKey, IDisposable> subsriber)
        {
            _item = item;
            _key = key;
            _cleanUp = subsriber(item, key);
        }

        public void Dispose()
        {
            _cleanUp.Dispose();
        }

        #region Equality

        public bool Equals(SubscriptionContainer<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(_key, other._key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SubscriptionContainer<TObject, TKey>)obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<TKey>.Default.GetHashCode(_key);
        }

        public static bool operator ==(SubscriptionContainer<TObject, TKey> left, SubscriptionContainer<TObject, TKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SubscriptionContainer<TObject, TKey> left, SubscriptionContainer<TObject, TKey> right)
        {
            return !Equals(left, right);
        }

        #endregion

        public override string ToString()
        {
            return $"Key: {_key}, Item: {_item}";
        }
    }
}
