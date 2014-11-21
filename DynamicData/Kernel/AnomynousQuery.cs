using System.Collections.Generic;

namespace DynamicData.Kernel
{
    internal class AnomynousQuery<TObject, TKey> : IQuery<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;

        public AnomynousQuery(ICache<TObject, TKey> cache)
        {
            _cache = cache;
        }

        public AnomynousQuery(Cache<TObject, TKey> cache)
        {
            _cache = cache;
        }

        public int Count
        {
            get { return _cache.Count; }
        }

        public IEnumerable<TObject> Items
        {
            get { return _cache.Items; }
        }

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues
        {
            get { return _cache.KeyValues; }
        }

        public IEnumerable<TKey> Keys
        {
            get { return _cache.Keys; }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }
    }
}