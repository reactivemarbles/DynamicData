using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{
    internal class Cache<TObject, TKey> : ICache<TObject, TKey>
    {
        private readonly IDictionary<TKey, TObject> _data;

        public Cache()
        {
            _data = new Dictionary<TKey, TObject>();
        }

        public Cache(IDictionary<TKey, TObject>  dictionary)
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
            _data[item.Key] = item.Value;
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            _data[key] = item;
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
            if (_data.ContainsKey(item.Key))
            {
                _data.Remove(item.Key);
            }
        }

        public void Remove(TKey key)
        {
            if (_data.ContainsKey(key))
            {
                _data.Remove(key);
            }
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
            get
            {
                 return _data;
            }
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