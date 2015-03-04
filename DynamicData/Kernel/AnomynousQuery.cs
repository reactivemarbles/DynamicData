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