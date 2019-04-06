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
    public sealed class  ChangeAwareCache<TObject, TKey> : ICache<TObject, TKey>
    {
        private ChangeSet<TObject, TKey> _changes;

        private Dictionary<TKey, TObject> _data;

        /// <inheritdoc />
        public int Count => _data?.Count ?? 0;

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data ?? Enumerable.Empty<KeyValuePair<TKey, TObject>>();

        /// <inheritdoc />
        public IEnumerable<TObject> Items => _data?.Values ?? Enumerable.Empty<TObject>();

        /// <inheritdoc />
        public IEnumerable<TKey> Keys => _data?.Keys ?? Enumerable.Empty<TKey>();

        /// <inheritdoc />
        public ChangeAwareCache()
        {
        }

        /// <inheritdoc />
        public ChangeAwareCache(int capacity)
        {
            EnsureInitialised(capacity);
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
        public ChangeAwareCache(Dictionary<TKey, TObject> data)
        {
            _data = data;
        }

        /// <inheritdoc />
        public Optional<TObject> Lookup(TKey key) => _data?.Lookup(key) ?? Optional<TObject>.None;

        /// <summary>
        /// Adds the item to the cache without checking whether there is an existing value in the cache
        /// </summary>
        public void Add(TObject item, TKey key)
        {
            EnsureInitialised();
            _changes.Add(new Change<TObject, TKey>(ChangeReason.Add, key, item));
            _data.Add(key, item);
        }

        /// <inheritdoc />
        public void AddOrUpdate(TObject item, TKey key)
        {
            EnsureInitialised();

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
            if (_data == null) return;

            if (keys is IList<TKey> list)
            {
                EnsureInitialised(list.Count);
                var enumerable = EnumerableIList.Create(list);
                foreach (var item in enumerable)
                    Remove(item);
            }
            else
            {
                EnsureInitialised();
                foreach (var key in keys)
                    Remove(key);
            }
        }

        /// <inheritdoc />
        public void Remove(TKey key)
        {
            if (_data == null) return;

            if (_data.TryGetValue(key, out var existingItem))
            {
                EnsureInitialised();
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, existingItem));
                _data.Remove(key);
            }
        }



        /// <summary>
        /// Raises an evaluate change for the specified keys
        /// </summary>
        public void Refresh(IEnumerable<TKey> keys)
        {
            if (keys is IList<TKey> list)
            {
                EnsureInitialised(list.Count);
                var enumerable = EnumerableIList.Create(list);
                foreach (var key in enumerable)
                    Refresh(key);
            }
            else
            {
                EnsureInitialised();
                foreach (var key in keys)
                    Refresh(key);
            }
        }

        /// <summary>
        /// Raises an evaluate change for all items in the cache
        /// </summary>
        public void Refresh()
        {
            EnsureInitialised(_data.Count);
            _changes.AddRange(_data.Select(t => new Change<TObject, TKey>(ChangeReason.Refresh, t.Key, t.Value)));
        }


        /// <summary>
        /// Raises an evaluate change for the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        public void Refresh(TKey key)
        {
            EnsureInitialised();
            if (_data.TryGetValue(key, out var existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Refresh, key, existingItem));
            }
        }


        /// <inheritdoc />
        public void Clear()
        {
            if (_data == null) return;

            EnsureInitialised(_data.Count);

            var toremove = _data.Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value));
            _changes.AddRange(toremove);
            _data.Clear();
        }
        
        /// <inheritdoc />
        public void Clone(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            EnsureInitialised(changes.Count);

            var enumerable = changes.ToConcreteType();
            foreach (var change in enumerable)
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

        private void EnsureInitialised(int capacity = -1)
        {
            if (_changes == null)
                _changes = capacity > 0 ? new ChangeSet<TObject, TKey>(capacity) : new ChangeSet<TObject, TKey>();

            if (_data == null)
                _data = capacity > 0 ? new Dictionary<TKey, TObject>(capacity) : new Dictionary<TKey, TObject>();
        }

        /// <summary>
        /// Create a changeset from recorded changes and clears known changes.
        /// </summary>
        public ChangeSet<TObject, TKey> CaptureChanges()
        {
            if (_changes == null || _changes.Count==0)
                return ChangeSet<TObject, TKey>.Empty;

            var copy = _changes;
            _changes = null;
            return copy;
        }

    }
}