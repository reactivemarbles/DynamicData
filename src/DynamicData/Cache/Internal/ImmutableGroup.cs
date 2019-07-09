using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class ImmutableGroup<TObject, TKey, TGroupKey> : IGrouping<TObject, TKey, TGroupKey>, IEquatable<ImmutableGroup<TObject, TKey, TGroupKey>>
    {
        private readonly ICache<TObject, TKey> _cache;

        public TGroupKey Key { get;  }

        internal ImmutableGroup(TGroupKey key, ICache<TObject, TKey> cache)
        {
            Key = key;
            _cache = new Cache<TObject, TKey>(cache.Count);
            cache.KeyValues.ForEach(kvp => _cache.AddOrUpdate(kvp.Value, kvp.Key));
        }

        public int Count => _cache.Count;
        public IEnumerable<TObject> Items => _cache.Items;
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;
        public IEnumerable<TKey> Keys => _cache.Keys;

        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }

        #region Equality

        public bool Equals(ImmutableGroup<TObject, TKey, TGroupKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ImmutableGroup<TObject, TKey, TGroupKey> && Equals((ImmutableGroup<TObject, TKey, TGroupKey>) obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<TGroupKey>.Default.GetHashCode(Key);
        }

        public static bool operator ==(ImmutableGroup<TObject, TKey, TGroupKey> left, ImmutableGroup<TObject, TKey, TGroupKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ImmutableGroup<TObject, TKey, TGroupKey> left, ImmutableGroup<TObject, TKey, TGroupKey> right)
        {
            return !Equals(left, right);
        }

        #endregion

        public override string ToString()
        {
            return $"Grouping for: {Key} ({Count} items)";
        }
    }
}