using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class ChangeAwareCache<TObject, TKey>: ICache<TObject, TKey>
    {
        private List<Change<TObject, TKey>> _changes = new List<Change<TObject, TKey>>();

        private Dictionary<TKey, TObject> _data;

        public int Count => _data.Count;
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;
        public IEnumerable<TObject> Items => _data.Values;
        public IEnumerable<TKey> Keys => _data.Keys;

        public ChangeAwareCache()
        {
            _data = new Dictionary<TKey, TObject>();
        }


        public Optional<TObject> Lookup(TKey key)
        {
            return _data.Lookup(key);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            TObject existingItem;

            _changes.Add(_data.TryGetValue(key, out existingItem)
                ? new Change<TObject, TKey>(ChangeReason.Update, key, item, existingItem)
                : new Change<TObject, TKey>(ChangeReason.Add, key, item));

            _data[key] = item;
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            keys.ForEach(Remove);
        }

        public void Remove(TKey key)
        {
            TObject existingItem;
            if (_data.TryGetValue(key, out existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, existingItem));
                _data.Remove(key);
            }
        }

        public void Evaluate()
        {
            _changes.Capacity = _data.Count + _changes.Count;
            _changes.AddRange(_data.Select(t => new Change<TObject, TKey>(ChangeReason.Evaluate, t.Key, t.Value)));
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            keys.ForEach(Evaluate);
        }

        public void Evaluate(TKey key)
        {
            TObject existingItem;
            if (_data.TryGetValue(key, out existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Evaluate, key, existingItem));
            }
        }

        public void Clear()
        {
            var toremove = _data.Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value)).ToArray();
            _changes.AddRange(toremove);
            _data.Clear();
        }

        public void Clone(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            //for efficiency resize dictionary to initial batch size
            if (_data.Count == 0)
                _data = new Dictionary<TKey, TObject>(changes.Count);

            _changes.Capacity = changes.Count + _changes.Count;

            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                        AddOrUpdate(change.Current, change.Key);
                        break;
                    case ChangeReason.Remove:
                        Remove(change.Key);
                        break;
                    case ChangeReason.Evaluate:
                        Evaluate(change.Key);
                        break;
                }
            }
        }


        public ChangeSet<TObject, TKey> CaptureChanges()
        {
            var copy = new ChangeSet<TObject, TKey>(_changes);
            _changes = new List<Change<TObject, TKey>>();
            return copy;
        }
    }
}