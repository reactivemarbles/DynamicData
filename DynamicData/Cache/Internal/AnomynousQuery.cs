using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal class AnomynousQuery<TObject, TKey> : IQuery<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;

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

    //internal class AnomynousList<T> : IReadOnlyCollection<T>
    //{
    //	private readonly IList<T> _list;

    //	public AnomynousList(IList<T> list)
    //	{
    //		_list = list;
    //	}

    //	public int Count => _list.Count;

    //	public IEnumerable<T> Items => _list;

    //	public Optional<T> Lookup(TKey key)
    //	{
    //		return _list.Lookup(key);
    //	}
    //}
}
