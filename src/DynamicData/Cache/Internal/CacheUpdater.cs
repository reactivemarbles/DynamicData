// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the CacheUpdater class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class CacheUpdater<TObject, TKey> : ISourceUpdater<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly ICache<TObject, TKey> _cache;

    /// <summary>
    /// The _keySelector field.
    /// </summary>
    private readonly Func<TObject, TKey>? _keySelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheUpdater{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    public CacheUpdater(ICache<TObject, TKey> cache, Func<TObject, TKey>? keySelector = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _keySelector = keySelector;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheUpdater{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="data">The data value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    public CacheUpdater(Dictionary<TKey, TObject> data, Func<TObject, TKey>? keySelector = null)
    {
        ArgumentExceptionHelper.ThrowIfNull(data);

        _cache = new Cache<TObject, TKey>(data);
        _keySelector = keySelector;
    }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IEnumerable<TObject> Items => _cache.Items;

    /// <summary>
    /// Gets the Keys value.
    /// </summary>
    public IEnumerable<TKey> Keys => _cache.Keys;

    /// <summary>
    /// Gets the KeyValues value.
    /// </summary>
    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    public void AddOrUpdate(IEnumerable<TObject> items)
    {
        ArgumentExceptionHelper.ThrowIfNull(items);

        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        if (items is IList<TObject> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                _cache.AddOrUpdate(item, _keySelector(item));
            }
        }
        else
        {
            foreach (var item in items)
            {
                _cache.AddOrUpdate(item, _keySelector(item));
            }
        }
    }

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <param name="comparer">The comparer value.</param>
    public void AddOrUpdate(IEnumerable<TObject> items, IEqualityComparer<TObject> comparer)
    {
        ArgumentExceptionHelper.ThrowIfNull(items);
        ArgumentExceptionHelper.ThrowIfNull(comparer);

        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        void AddOrUpdateImpl(TObject item)
        {
            var key = _keySelector!(item);
            var oldItem = _cache.Lookup(key);

            if (oldItem.HasValue)
            {
                if (comparer.Equals(oldItem.Value, item))
                {
                    return;
                }

                _cache.AddOrUpdate(item, key);
            }
            else
            {
                _cache.AddOrUpdate(item, key);
            }
        }

        if (items is IList<TObject> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                AddOrUpdateImpl(item);
            }
        }
        else
        {
            foreach (var item in items)
            {
                AddOrUpdateImpl(item);
            }
        }
    }

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void AddOrUpdate(TObject item)
    {
        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        var key = _keySelector(item);
        _cache.AddOrUpdate(item, key);
    }

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <param name="comparer">The comparer value.</param>
    public void AddOrUpdate(TObject item, IEqualityComparer<TObject> comparer)
    {
        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        var key = _keySelector(item);
        var oldItem = _cache.Lookup(key);
        if (oldItem.HasValue)
        {
            if (comparer.Equals(oldItem.Value, item))
            {
                return;
            }

            _cache.AddOrUpdate(item, key);
            return;
        }

        _cache.AddOrUpdate(item, key);
    }

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="itemsPairs">The itemsPairs value.</param>
    public void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> itemsPairs)
    {
        if (itemsPairs is IList<KeyValuePair<TKey, TObject>> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                _cache.AddOrUpdate(item.Value, item.Key);
            }
        }
        else
        {
            foreach (var item in itemsPairs)
            {
                _cache.AddOrUpdate(item.Value, item.Key);
            }
        }
    }

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void AddOrUpdate(KeyValuePair<TKey, TObject> item) => _cache.AddOrUpdate(item.Value, item.Key);

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <param name="key">The key value.</param>
    public void AddOrUpdate(TObject item, TKey key) => _cache.AddOrUpdate(item, key);

    /// <summary>
    /// Executes the Clear operation.
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Executes the Clone operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    public void Clone(IChangeSet<TObject, TKey> changes) => _cache.Clone(changes);

    /// <summary>
    /// Executes the GetKey operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public TKey GetKey(TObject item)
    {
        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        return _keySelector(item);
    }

    /// <summary>
    /// Executes the GetKeyValues operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <returns>The result of the operation.</returns>
    public IEnumerable<KeyValuePair<TKey, TObject>> GetKeyValues(IEnumerable<TObject> items)
    {
        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        return items.Select(t => new KeyValuePair<TKey, TObject>(_keySelector(t), t));
    }

    /// <summary>
    /// Executes the Load operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    public void Load(IEnumerable<TObject> items)
    {
        ArgumentExceptionHelper.ThrowIfNull(items);

        Clear();
        AddOrUpdate(items);
    }

    /// <summary>
    /// Executes the Lookup operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key)
    {
        var item = _cache.Lookup(key);
        return item.HasValue ? item.Value : ReactiveUI.Primitives.Optional<TObject>.None;
    }

    /// <summary>
    /// Executes the Lookup operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TObject item)
    {
        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        var key = _keySelector(item);
        return Lookup(key);
    }

    /// <summary>
    /// Executes the Refresh operation.
    /// </summary>
    public void Refresh() => _cache.Refresh();

    /// <summary>
    /// Executes the Refresh operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    public void Refresh(IEnumerable<TObject> items)
    {
        ArgumentExceptionHelper.ThrowIfNull(items);

        if (items is IList<TObject> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                Refresh(item);
            }
        }
        else
        {
            foreach (var item in items)
            {
                Refresh(item);
            }
        }
    }

    /// <summary>
    /// Executes the Refresh operation.
    /// </summary>
    /// <param name="keys">The keys value.</param>
    public void Refresh(IEnumerable<TKey> keys)
    {
        ArgumentExceptionHelper.ThrowIfNull(keys);

        if (keys is IList<TKey> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                Refresh(item);
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
    /// Executes the Refresh operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Refresh(TObject item)
    {
        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        var key = _keySelector(item);
        _cache.Refresh(key);
    }

    /// <summary>
    /// Executes the Refresh operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    public void Refresh(TKey key) => _cache.Refresh(key);

    /// <summary>
    /// Executes the Remove operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    public void Remove(IEnumerable<TObject> items)
    {
        ArgumentExceptionHelper.ThrowIfNull(items);

        if (items is IList<TObject> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                Remove(item);
            }
        }
        else
        {
            foreach (var item in items)
            {
                Remove(item);
            }
        }
    }

    /// <summary>
    /// Executes the Remove operation.
    /// </summary>
    /// <param name="keys">The keys value.</param>
    public void Remove(IEnumerable<TKey> keys)
    {
        ArgumentExceptionHelper.ThrowIfNull(keys);

        if (keys is IList<TKey> list)
        {
            // zero allocation enumerator
            foreach (var key in EnumerableIList.Create(list))
            {
                Remove(key);
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

    /// <summary>
    /// Executes the Remove operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Remove(TObject item)
    {
        if (_keySelector is null)
        {
            throw new KeySelectorException("A key selector must be specified");
        }

        var key = _keySelector(item);
        _cache.Remove(key);
    }

    /// <summary>
    /// Executes the Remove operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    public void Remove(TKey key) => _cache.Remove(key);

    /// <summary>
    /// Executes the Remove operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    public void Remove(IEnumerable<KeyValuePair<TKey, TObject>> items)
    {
        ArgumentExceptionHelper.ThrowIfNull(items);

        if (items is IList<TObject> list)
        {
            // zero allocation enumerator
            foreach (var key in EnumerableIList.Create(list))
            {
                Remove(key);
            }
        }
        else
        {
            foreach (var key in items)
            {
                Remove(key);
            }
        }
    }

    /// <summary>
    /// Executes the Remove operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    public void Remove(KeyValuePair<TKey, TObject> item) => Remove(item.Key);

    /// <summary>
    /// Executes the RemoveKey operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    public void RemoveKey(TKey key) => Remove(key);

    /// <summary>
    /// Executes the RemoveKeys operation.
    /// </summary>
    /// <param name="keys">The keys value.</param>
    public void RemoveKeys(IEnumerable<TKey> keys)
    {
        ArgumentExceptionHelper.ThrowIfNull(keys);

        _cache.Remove(keys);
    }

    /// <summary>
    /// Executes the Update operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    public void Update(IChangeSet<TObject, TKey> changes) => _cache.Clone(changes);
}
