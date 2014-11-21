using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{
    internal class SourceUpdater<TObject, TKey> : ISourceUpdater<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;
        private readonly IKeySelector<TObject, TKey> _keySelector;
        private readonly Queue<Change<TObject, TKey>> _queue = new Queue<Change<TObject, TKey>>();

        public SourceUpdater(ICache<TObject, TKey> cache, IKeySelector<TObject, TKey> keySelector)
        {
            if (cache == null) throw new ArgumentNullException("cache");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            _cache = cache;
            _keySelector = keySelector;
        }

        public IEnumerable<TObject> Items
        {
            get { return _cache.Items; }
        }

        public IEnumerable<TKey> Keys
        {
            get { return _cache.Keys; }
        }

        public IEnumerable<KeyValuePair<TKey,TObject>> KeyValues
        {
            get { return _cache.KeyValues; }
        }
        
        public Optional<TObject> Lookup(TKey key)
        {
            Optional<TObject> item = _cache.Lookup(key);
            return item.HasValue ? item.Value : Optional.None<TObject>();
        }

        public void Load(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            Clear();
            AddOrUpdate(items);
        }

        public void AddOrUpdate(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException("items");

            items.ForEach(AddOrUpdate);
        }
        
        public void AddOrUpdate(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            var previous = _cache.Lookup(key);
            _queue.Enqueue(previous.HasValue
                               ? new Change<TObject, TKey>(ChangeReason.Update, key, item, previous)
                               : new Change<TObject, TKey>(ChangeReason.Add, key, item));
            _cache.AddOrUpdate(item, key);
        }

        public void Evaluate()
        {
            var requery = _cache.KeyValues.Select(t => new Change<TObject, TKey>(ChangeReason.Evaluate, t.Key, t.Value));
            requery.ForEach(_queue.Enqueue);
        }

        public void Evaluate(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            items.ForEach(Evaluate);
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException("keys");
            keys.ForEach(Evaluate);
        }
        
        
        public void Evaluate(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            var existing = _cache.Lookup(key);
            if (existing.HasValue)
            {
                _queue.Enqueue(new Change<TObject, TKey>(ChangeReason.Evaluate, key, item));
            }
        }

        public void Evaluate(TKey key)
        {
            var existing = _cache.Lookup(key);
            if (existing.HasValue)
            {
                _queue.Enqueue(new Change<TObject, TKey>(ChangeReason.Evaluate, key, existing.Value));
            }
        }

        public void Remove(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            items.ForEach(Remove);
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException("keys");
            keys.ForEach(Remove);
        }

        public void Remove(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            Remove(key);
        }

        public void Remove(TKey key)
        {
            Optional<TObject> existing = _cache.Lookup(key);
            if (existing.HasValue)
            {
                _queue.Enqueue(new Change<TObject, TKey>(ChangeReason.Remove, key, existing.Value));
                _cache.Remove(key);
            }
        }


        public void Clear()
        {
            var toremove = _cache.KeyValues.Select(
                    t => new Change<TObject, TKey>(ChangeReason.Remove, t.Key, t.Value));
            toremove.ForEach(_queue.Enqueue);
            _cache.Clear();
        }

        public int Count
        {
            get { return _cache.Count; }
        }

        public Optional<TObject> Lookup(TObject item)
        {
            TKey key = _keySelector.GetKey(item);
            return Lookup(key);
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
                            AddOrUpdate(item.Current);
                        }
                        break;
                    case ChangeReason.Remove:
                        Remove(item.Current);
                        break;
                    case ChangeReason.Evaluate:
                        Evaluate(item.Current);
                        break;
                }
            }
        }

        public IChangeSet<TObject, TKey> AsChangeSet()
        {

            var updates =  new ChangeSet<TObject, TKey>( _queue.ToArray());
            _queue.Clear();
            return updates;
        }
    }

}