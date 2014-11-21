using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    internal class ConcurrentCache<TObject, TKey> : ICache<TObject, TKey>
    {
        private readonly ConcurrentDictionary<TKey, TObject> _data;

        public ConcurrentCache()
        {
            _data =new ConcurrentDictionary<TKey, TObject>();
        }

        public ConcurrentCache(ConcurrentDictionary<TKey, TObject> dictionary)
        {
            _data = dictionary;
        }


        public Optional<TObject> Lookup(TKey key)
        {
            return _data.Lookup(key);
        }

        public void Load(IEnumerable<KeyValuePair<TKey,TObject>> items)
        {
            Clear();
            AddOrUpdate(items);
        }

        public void AddOrUpdate(IEnumerable<KeyValuePair<TKey,TObject>> items)
        {
            items.ForEach(AddOrUpdate);
        }

        public void AddOrUpdate(KeyValuePair<TKey,TObject> item)
        {
            _data.AddOrUpdate(item.Key, item.Value, (key, oldValue) => item.Value);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            _data.AddOrUpdate(key, item, (k, oldValue) => item);
        }

        public void Remove(IEnumerable<KeyValuePair<TKey,TObject>> items)
        {
            items.ForEach(Remove);
        }

        public void Remove(IEnumerable<TKey> items)
        {
            items.ForEach(Remove);
        }

        public void Remove(KeyValuePair<TKey,TObject> item)
        {
            TObject removed;
            _data.TryRemove(item.Key, out removed);
        }

        public void Remove(TKey key)
        {
            TObject removed;
            _data.TryRemove(key, out removed);
        }


        public void Clear()
        {
            _data.Clear();
        }

        public int Count
        {
            get { return _data.Count; }
        }

        public IEnumerable<KeyValuePair<TKey,TObject>> KeyValues
        {
            get { return _data; }
        }

        public IEnumerable<TObject> Items
        {
            get { return _data.Values; }
        }

        public IEnumerable<TKey> Keys
        {
            get { return _data.Keys; }
        }
    }
}