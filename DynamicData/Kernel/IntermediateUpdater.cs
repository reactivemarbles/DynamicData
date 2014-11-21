using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{
    internal class IntermediateUpdater<TObject, TKey> : IIntermediateUpdater<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;
        private  ChangeSet<TObject, TKey> _queue = new ChangeSet<TObject, TKey>();


        public IntermediateUpdater(ICache<TObject, TKey> cache)
        {
            if (cache == null) throw new ArgumentNullException("cache");
            _cache = cache;
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
            AddOrUpdate(item,key);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            Optional<TObject> previous = _cache.Lookup(key);
            _cache.AddOrUpdate(item, key);
            _queue.Add(previous.HasValue
                               ? new Change<TObject, TKey>(ChangeReason.Update, key, item, previous)
                               : new Change<TObject, TKey>(ChangeReason.Add, key, item));
        }

        #region Evaluate


        public void Evaluate()
        {
            var toevaluate =_cache.KeyValues.Select(t => new Change<TObject, TKey>(ChangeReason.Evaluate, t.Key, t.Value));
            toevaluate.ForEach(_queue.Add);
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            keys.ForEach(Evaluate);
        }

        public void Evaluate(TKey key)
        {
            Optional<TObject> existing = _cache.Lookup(key);
            if (existing.HasValue)
            {
                _queue.Add(new Change<TObject, TKey>(ChangeReason.Evaluate, key, existing.Value));
            }
        }


        #endregion

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
            Optional<TObject> existing = _cache.Lookup(key);
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

        public int Count
        {
            get { return _cache.Count; }
        }
        

        public void Update(IChangeSet<TObject, TKey> updates)
        {
            if (updates == null) throw new ArgumentNullException("updates");
            foreach (var item in updates)
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
                        Remove(item.Key);
                        break;
                    case ChangeReason.Evaluate:
                        Evaluate(item.Key);
                        break;
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