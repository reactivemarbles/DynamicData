using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class AnonymousObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        private readonly IObservableCache<TObject, TKey> _cache;

        public AnonymousObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _cache = new ObservableCache<TObject, TKey>(source);
        }

        public AnonymousObservableCache(IObservableCache<TObject, TKey> cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _cache = cache;
        }

        public IObservable<int> CountChanged => _cache.CountChanged;

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _cache.Watch(key);
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            return _cache.Connect(predicate);
        }

        public IEnumerable<TKey> Keys => _cache.Keys;

        public IEnumerable<TObject> Items => _cache.Items;

        public int Count => _cache.Count;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }
    }
}
