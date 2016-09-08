using System.Collections.Generic;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class AnonymousQuery<TObject, TKey> : IQuery<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;

        public AnonymousQuery(Cache<TObject, TKey> cache)
        {
            _cache = cache;
        }

        public int Count => _cache.Count;

        public IEnumerable<TObject> Items => _cache.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

        public IEnumerable<TKey> Keys => _cache.Keys;

        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }
    }
}
