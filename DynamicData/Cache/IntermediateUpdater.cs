using System;
using System.Collections.Generic;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData
{
    internal class IntermediateUpdater<TObject, TKey> : IIntermediateUpdater<TObject, TKey>
    {
        private readonly ChangeAwareCache<TObject, TKey> _cache;

        public IEnumerable<TObject> Items => _cache.Items;
        public IEnumerable<TKey> Keys => _cache.Keys;
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;
        public int Count => _cache.Count;

        public IntermediateUpdater(ChangeAwareCache<TObject, TKey> cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _cache = cache;
        }

        public IntermediateUpdater()
        {
            _cache = new ChangeAwareCache<TObject, TKey>();
        }


        public Optional<TObject> Lookup(TKey key)
        {
            var item = _cache.Lookup(key);
            return item.HasValue ? item.Value : Optional.None<TObject>();
        }

        public void Load(IEnumerable<TObject> items, Func<TObject, TKey> keySelector)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            Clear();
            AddOrUpdate(items, keySelector);
        }

        public void AddOrUpdate(IEnumerable<TObject> items, Func<TObject, TKey> keySelector)
        {
            items.ForEach(t => AddOrUpdate(t, keySelector));
        }

        public void AddOrUpdate(TObject item, Func<TObject, TKey> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var key = keySelector(item);
            AddOrUpdate(item, key);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            _cache.AddOrUpdate(item, key);
        }

        public void Evaluate()
        {
            _cache.Evaluate();
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            _cache.Evaluate(keys);
        }

        public void Evaluate(TKey key)
        {
            _cache.Evaluate(key);
        }

        public void Remove(IEnumerable<TObject> items, Func<TObject, TKey> keySelector)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            items.ForEach(t => Remove(keySelector(t)));
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            keys.ForEach(Remove);
        }

        public void Remove(TKey key)
        {
            _cache.Remove(key);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void Update(IChangeSet<TObject, TKey> changes)
        {
            _cache.Clone(changes);
        }

        public IChangeSet<TObject, TKey> AsChangeSet()
        {
            return _cache.CaptureChanges();
        }
    }
}
