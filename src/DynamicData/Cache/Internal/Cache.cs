// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

[DebuggerDisplay("Cache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
internal sealed class Cache<TObject, TKey> : ICache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public static readonly Cache<TObject, TKey> Empty = new();

    private readonly Dictionary<TKey, TObject> _data;

    public Cache(int capacity = -1) =>
        _data = capacity > 1 ? new Dictionary<TKey, TObject>(capacity) : [];

    public Cache(Dictionary<TKey, TObject> data) => _data = data;

    public int Count => _data.Count;

    public IEnumerable<TObject> Items => _data.Values;

    public IEnumerable<TKey> Keys => _data.Keys;

    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;

    public void AddOrUpdate(TObject item, TKey key) => _data[key] = item;

    public void Clear() => _data.Clear();

    public Cache<TObject, TKey> Clone() => new(new Dictionary<TKey, TObject>(_data));

    public void Clone(IChangeSet<TObject, TKey> changes)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        foreach (var item in changes.ToConcreteType())
        {
            switch (item.Reason)
            {
                case ChangeReason.Update:
                case ChangeReason.Add:
                    {
                        _data[item.Key] = item.Current;
                    }

                    break;

                case ChangeReason.Remove:
                    _data.Remove(item.Key);
                    break;
            }
        }
    }

    public Optional<TObject> Lookup(TKey key) => _data.Lookup(key);

    /// <summary>
    /// Sends a signal for operators to recalculate it's state.
    /// </summary>
    public void Refresh()
    {
    }

    /// <summary>
    /// Refreshes the items matching the specified keys.
    /// </summary>
    /// <param name="keys">The keys.</param>
    public void Refresh(IEnumerable<TKey> keys)
    {
    }

    /// <summary>
    /// Refreshes the item matching the specified key.
    /// </summary>
    /// <param name="key">The key to refresh.</param>
    public void Refresh(TKey key)
    {
    }

    public void Remove(IEnumerable<TKey> keys)
    {
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

    public void Remove(TKey key) => _data.Remove(key);
}
