using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal class SourceUpdater<TObject, TKey> : ISourceUpdater<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;
        private readonly IKeySelector<TObject, TKey> _keySelector;
        private ChangeSet<TObject, TKey> _queue = new ChangeSet<TObject, TKey>();

        public SourceUpdater(ICache<TObject, TKey> cache, IKeySelector<TObject, TKey> keySelector)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            _cache = cache;
            _keySelector = keySelector;
        }

        public IEnumerable<TObject> Items => _cache.Items;

        public IEnumerable<TKey> Keys => _cache.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

        public Optional<TObject> Lookup(TKey key)
        {
            var item = _cache.Lookup(key);
            return item.HasValue ? item.Value : Optional.None<TObject>();
        }

        public void Load(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            Clear();
            AddOrUpdate(items);
        }

        public void AddOrUpdate(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            items.ForEach(AddOrUpdate);
        }

        public void AddOrUpdate(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            var previous = _cache.Lookup(key);
            _queue.Add(previous.HasValue
                ? new Change<TObject, TKey>(ChangeReason.Update, key, item, previous)
                : new Change<TObject, TKey>(ChangeReason.Add, key, item));
            _cache.AddOrUpdate(item, key);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            var previous = _cache.Lookup(key);
            _queue.Add(previous.HasValue
                ? new Change<TObject, TKey>(ChangeReason.Update, key, item, previous)
                : new Change<TObject, TKey>(ChangeReason.Add, key, item));
            _cache.AddOrUpdate(item, key);
        }

        public void Evaluate()
        {
            var requery = _cache.KeyValues.Select(t => new Change<TObject, TKey>(ChangeReason.Evaluate, t.Key, t.Value));
            requery.ForEach(_queue.Add);
        }

        public void Evaluate(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            items.ForEach(Evaluate);
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            keys.ForEach(Evaluate);
        }

        public void Evaluate(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            var existing = _cache.Lookup(key);
            if (existing.HasValue)
                _queue.Add(new Change<TObject, TKey>(ChangeReason.Evaluate, key, item));
        }

        public void Evaluate(TKey key)
        {
            var existing = _cache.Lookup(key);
            if (existing.HasValue)
                _queue.Add(new Change<TObject, TKey>(ChangeReason.Evaluate, key, existing.Value));
        }

        public void Remove(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            items.ForEach(Remove);
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            keys.ForEach(Remove);
        }

        public void RemoveKeys(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            keys.ForEach(RemoveKey);
        }

        public void Remove(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            Remove(key);
        }

        public void Remove(TKey key)
        {
            var existing = _cache.Lookup(key);
            if (!existing.HasValue) return;
            _queue.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, existing.Value));
            _cache.Remove(key);
        }

        public void RemoveKey(TKey key)
        {
            Remove(key);
        }

        public void Clear()
        {
            var toremove = _cache.KeyValues.Select(t => new Change<TObject, TKey>(ChangeReason.Remove, t.Key, t.Value));
            toremove.ForEach(_queue.Add);
            _cache.Clear();
        }

        public int Count { get { return _cache.Count; } }

        public Optional<TObject> Lookup(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            return Lookup(key);
        }

        public void Update(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
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
                        break;
                    }
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
            var result = _queue;
            _queue = new ChangeSet<TObject, TKey>();
            return result;
        }
    }
}
