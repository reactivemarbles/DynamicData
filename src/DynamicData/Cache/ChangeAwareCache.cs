// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// A cache which captures all changes which are made to it. These changes are recorded until CaptureChanges() at which point thw changes are cleared.
/// Used for creating custom operators.
/// </summary>
/// <seealso cref="ICache{TObject, TKey}" />
/// <typeparam name="TObject">The value of the cache.</typeparam>
/// <typeparam name="TKey">The key of the cache.</typeparam>
public sealed class ChangeAwareCache<TObject, TKey> : ICache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Dictionary<TKey, TObject> _data;
    private ChangeSet<TObject, TKey> _changes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareCache{TObject, TKey}"/> class.
    /// </summary>
    public ChangeAwareCache()
    {
        _changes = [];
        _data = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="capacity">The capacity of the initial items.</param>
    public ChangeAwareCache(int capacity)
    {
        _changes = new ChangeSet<TObject, TKey>(capacity);
        _data = new Dictionary<TKey, TObject>(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="data">Data to populate the cache with.</param>
    public ChangeAwareCache(Dictionary<TKey, TObject> data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _changes = [];
    }

    /// <inheritdoc />
    public int Count => _data.Count;

    /// <inheritdoc />
    public IEnumerable<TObject> Items => _data.Values;

    /// <inheritdoc />
    public IEnumerable<TKey> Keys => _data.Keys;

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;

    internal Dictionary<TKey, TObject> GetDictionary() => _data;

    /// <summary>
    /// Adds the item to the cache without checking whether there is an existing value in the cache.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="key">The key to add.</param>
    public void Add(TObject item, TKey key)
    {
        _changes.Add(new Change<TObject, TKey>(ChangeReason.Add, key, item));
        _data.Add(key, item);
    }

    /// <inheritdoc />
    public void AddOrUpdate(TObject item, TKey key)
    {
        _changes.Add(_data.TryGetValue(key, out var existingItem) ? new Change<TObject, TKey>(ChangeReason.Update, key, item, existingItem) : new Change<TObject, TKey>(ChangeReason.Add, key, item));

        _data[key] = item;
    }

    /// <summary>
    /// Create a change set from recorded changes and clears known changes.
    /// </summary>
    /// <returns>A change set with the key/value changes.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "This would result in differing operation")]
    public ChangeSet<TObject, TKey> CaptureChanges()
    {
        if (_changes.Count == 0)
        {
            return ChangeSet<TObject, TKey>.Empty;
        }

        var copy = _changes;
        _changes = [];
        return copy;
    }

    /// <inheritdoc />
    public void Clear()
    {
        var toRemove = _data.Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value));
        _changes.AddRange(toRemove);
        _data.Clear();
    }

    /// <inheritdoc />
    public void Clone(IChangeSet<TObject, TKey> changes)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        foreach (var change in changes.ToConcreteType())
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
                case ChangeReason.Moved:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(changes));
            }
        }
    }

    /// <inheritdoc />
    public Optional<TObject> Lookup(TKey key) => _data.Lookup(key);

    /// <summary>
    /// Raises an evaluate change for the specified keys.
    /// </summary>
    /// <param name="keys">The keys to refresh.</param>
    public void Refresh(IEnumerable<TKey> keys)
    {
        keys.ThrowArgumentNullExceptionIfNull(nameof(keys));

        if (keys is IList<TKey> list)
        {
            foreach (var key in EnumerableIList.Create(list))
            {
                Refresh(key);
            }
        }
        else
        {
            foreach (var key in keys)
            {
                Refresh(key);
            }
        }
    }

    /// <summary>
    /// Raises an evaluate change for all items in the cache.
    /// </summary>
    public void Refresh() => _changes.AddRange(_data.Select(t => new Change<TObject, TKey>(ChangeReason.Refresh, t.Key, t.Value)));

    /// <summary>
    /// Raises an evaluate change for the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    public void Refresh(TKey key)
    {
        if (_data.TryGetValue(key, out var existingItem))
        {
            _changes.Add(new Change<TObject, TKey>(ChangeReason.Refresh, key, existingItem));
        }
    }

    /// <summary>
    /// Removes the item matching the specified keys.
    /// </summary>
    /// <param name="keys">The keys.</param>
    public void Remove(IEnumerable<TKey> keys)
    {
        keys.ThrowArgumentNullExceptionIfNull(nameof(keys));

        if (keys is IList<TKey> list)
        {
            foreach (var item in EnumerableIList.Create(list))
            {
                Remove(item);
            }
        }
        else
        {
            foreach (var key in keys)
            {
                Remove(key);
            }
        }
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
}
