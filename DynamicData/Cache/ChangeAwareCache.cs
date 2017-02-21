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

        /// <summary>
        /// Gets the count.
        /// </summary>
        public int Count => _data.Count;
        
        /// <summary>
        /// Gets the items together with their keys
        /// </summary>
        /// <value>
        /// The key values.
        /// </value>
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;
      
        /// <summary>
        /// Gets the items.
        /// </summary>
        public IEnumerable<TObject> Items => _data.Values;


        /// <summary>
        /// Gets the keys.
        /// </summary>
        public IEnumerable<TKey> Keys => _data.Keys;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeAwareCache{TObject, TKey}"/> class.
        /// </summary>
        public ChangeAwareCache()
        {
            _data = new Dictionary<TKey, TObject>();
        }

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup
        /// </remarks>
        /// <param name="key">The key.</param>
        public Optional<TObject> Lookup(TKey key) => _data.Lookup(key);

        /// <summary>
        /// Adds or updates the item using the specified key
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="key">The key.</param>
        public void AddOrUpdate(TObject item, TKey key)
        {
            TObject existingItem;

            _changes.Add(_data.TryGetValue(key, out existingItem)
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

        /// <summary>
        /// Removes the item matching the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        public void Remove(TKey key)
        {
            TObject existingItem;
            if (_data.TryGetValue(key, out existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, existingItem));
                _data.Remove(key);
            }
        }


        /// <summary>
        /// Raises an evaluate change for the specified keys
        /// </summary>
        public void Evaluate(IEnumerable<TKey> keys)
        {
            keys.ForEach(Evaluate);
        }

        /// <summary>
        /// Raises an evaluate change for all items in the cache
        /// </summary>
        public void Evaluate()
        {
            _changes.Capacity = _data.Count + _changes.Count;
            _changes.AddRange(_data.Select(t => new Change<TObject, TKey>(ChangeReason.Evaluate, t.Key, t.Value)));
        }


        /// <summary>
        /// Raises an evaluate change for the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        public void Evaluate(TKey key)
        {
            TObject existingItem;
            if (_data.TryGetValue(key, out existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Evaluate, key, existingItem));
            }
        }


        /// <summary>
        /// Clears all items
        /// </summary>
        public void Clear()
        {
            var toremove = _data.Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value)).ToArray();
            _changes.AddRange(toremove);
            _data.Clear();
        }

        /// <summary>
        /// Clones the cache from the specified changes
        /// </summary>
        /// <param name="changes">The changes.</param>
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