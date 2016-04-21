using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData
{
    internal class IntermediateUpdater<TObject, TKey> : IIntermediateUpdater<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;
        private ChangeSet<TObject, TKey> _queue = new ChangeSet<TObject, TKey>();

        public IEnumerable<TObject> Items => _cache.Items;
        public IEnumerable<TKey> Keys => _cache.Keys;
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;
        public int Count => _cache.Count;

        public IntermediateUpdater(ICache<TObject, TKey> cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _cache = cache;
        }

        public Optional<TObject> Lookup(TKey key)
        {
            var item = _cache.Lookup(key);
            return item.HasValue ? item.Value : Optional.None<TObject>();
        }

        public void Load(IEnumerable<TObject> items, Func<TObject, TKey> keySelector)
        {
            if (items == null) throw new ArgumentNullException("items");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            Clear();
            AddOrUpdate(items, keySelector);
        }

        public void AddOrUpdate(IEnumerable<TObject> items, Func<TObject, TKey> keySelector)
        {
            items.ForEach(t => AddOrUpdate(t, keySelector));
        }

        public void AddOrUpdate(TObject item, Func<TObject, TKey> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            var key = keySelector(item);
            AddOrUpdate(item, key);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            var previous = _cache.Lookup(key);
            _cache.AddOrUpdate(item, key);
            _queue.Add(previous.HasValue
                ? new Change<TObject, TKey>(ChangeReason.Update, key, item, previous)
                : new Change<TObject, TKey>(ChangeReason.Add, key, item));
        }

        public void Evaluate()
        {
            var toevaluate = _cache.KeyValues.Select(t => new Change<TObject, TKey>(ChangeReason.Evaluate, t.Key, t.Value));
            toevaluate.ForEach(_queue.Add);
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            keys.ForEach(Evaluate);
        }

        public void Evaluate(TKey key)
        {
            var existing = _cache.Lookup(key);
            if (existing.HasValue)
                _queue.Add(new Change<TObject, TKey>(ChangeReason.Evaluate, key, existing.Value));
        }

        public void Remove(IEnumerable<TObject> items, Func<TObject, TKey> keySelector)
        {
            if (items == null) throw new ArgumentNullException("items");
            items.ForEach(t => Remove(keySelector(t)));
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException("keys");
            keys.ForEach(Remove);
        }

        public void Remove(TKey key)
        {
            var existing = _cache.Lookup(key);
            if (existing.HasValue)
            {
                _queue.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, existing.Value));
                _cache.Remove(key);
            }
        }

        public void Clear()
        {
            var toremove = _cache.KeyValues.Select(t => new Change<TObject, TKey>(ChangeReason.Remove, t.Key, t.Value));
            toremove.ForEach(_queue.Add);
            _cache.Clear();
        }

        public void Update(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null) throw new ArgumentNullException("changes");
            foreach (var item in changes)
            {
                switch (item.Reason)
                {
                    case ChangeReason.Update:
                    case ChangeReason.Add:
                    {
                        AddOrUpdate(item.Current, item.Key);
                    }
                        break;
                    case ChangeReason.Remove:
                    {
                        var existing = _cache.Lookup(item.Key);
                        if (existing.HasValue)
                        {
                            _queue.Add(item);
                            _cache.Remove(item.Key);
                        }
                    }
                        Remove(item.Key);
                        break;
                    case ChangeReason.Evaluate:
                    {
                        var existing = _cache.Lookup(item.Key);
                        if (existing.HasValue) _queue.Add(item);
                        break;
                    }
                }
            }
        }

        public IChangeSet<TObject, TKey> AsChangeSet()
        {
            var copy = _queue;
            _queue = new ChangeSet<TObject, TKey>();
            return copy;
        }
    }
}
