using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;


// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A cache which captures all changes which are made to it. These changes are recorded until CaptureChanges() at which point thw changes are cleared.
    /// 
    /// Used for creating custom operators
    /// </summary>
    /// <seealso cref="DynamicData.ICache{TObject, TKey}" />
    public sealed class ChangeAwareCache<TObject, TKey>: ICache<TObject, TKey>
    {
        private List<Change<TObject, TKey>> _changes = new List<Change<TObject, TKey>>();

        private Dictionary<TKey, TObject> _data;

        /// <inheritdoc />
        public int Count => _data.Count;

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;
      
        /// <inheritdoc />
        public IEnumerable<TObject> Items => _data.Values;


        /// <inheritdoc />
        public IEnumerable<TKey> Keys => _data.Keys;

        /// <inheritdoc />
        public ChangeAwareCache()
        {
            _data = new Dictionary<TKey, TObject>();
        }

        /// <inheritdoc />
        public Optional<TObject> Lookup(TKey key) => _data.Lookup(key);

        /// <inheritdoc />
        public void AddOrUpdate(TObject item, TKey key)
        {
            _changes.Add(_data.TryGetValue(key, out var existingItem)
                ? new Change<TObject, TKey>(ChangeReason.Update, key, item, existingItem)
                : new Change<TObject, TKey>(ChangeReason.Add, key, item));

            _data[key] = item;
        }

        /// <summary>
        /// Removes the item matching the specified keys.
        /// </summary>
        /// <param name="keys">The keys.</param>
        public void Remove(IEnumerable<TKey> keys)
        {
            keys.ForEach(Remove);
        }

        /// <inheritdoc />
        public void Remove(TKey key)
        {
            if (_data.TryGetValue(key, out var existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, existingItem));
                _data.Remove(key);
            }
        }


        /// <summary>
        /// Raises an evaluate change for the specified keys
        /// </summary>
        public void Refresh(IEnumerable<TKey> keys)
        {
            keys.ForEach(Refresh);
        }

        /// <summary>
        /// Raises an evaluate change for all items in the cache
        /// </summary>
        public void Refresh()
        {
            _changes.Capacity = _data.Count + _changes.Count;
            _changes.AddRange(_data.Select(t => new Change<TObject, TKey>(ChangeReason.Refresh, t.Key, t.Value)));
        }


        /// <summary>
        /// Raises an evaluate change for the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        public void Refresh(TKey key)
        {
            if (_data.TryGetValue(key, out var existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Refresh, key, existingItem));
            }
        }


        /// <inheritdoc />
       public void Clear()
        {
            var toremove = _data.Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value));
            _changes.AddRange(toremove);
            _data.Clear();
        }

        /// <inheritdoc />
        public void Clone(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            //for efficiency resize dictionary to initial batch size
            if (_data.Count == 0)
                _data = new Dictionary<TKey, TObject>(changes.Count);

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
                    case ChangeReason.Refresh:
                        Refresh(change.Key);
                        break;
                }
            }
        }

        /// <summary>
        /// Create a changeset from recorded changes and clears known changes.
        /// </summary>
        public ChangeSet<TObject, TKey> CaptureChanges()
        {
            var copy = new ChangeSet<TObject, TKey>(_changes);
            _changes = new List<Change<TObject, TKey>>();
            return copy;
        }
    }
}