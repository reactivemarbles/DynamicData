// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// Manages Disposables by Key:
/// 1) Adding a disposable with the same key will dispose/replace the previous one.
/// 2) Adding when the container is Disposed will Dispose it immediately.
/// </summary>
/// <typeparam name="TKey">Type to use for the Key.</typeparam>
internal sealed class KeyedDisposable<TKey> : IDisposable
    where TKey : notnull
{
    private readonly Dictionary<TKey, IDisposable> _disposables = [];
    private bool _disposedValue;

    public int Count => _disposables.Count;

    public IEnumerable<TKey> Keys => _disposables.Keys;

    public bool ContainsKey(TKey key) => _disposables.ContainsKey(key);

    public bool IsDisposed => _disposedValue;

    /// <summary>
    /// Tracks an item by key. If the item implements <see cref="IDisposable"/>,
    /// it replaces any existing entry (disposing the previous one if different).
    /// If the item is NOT disposable, any existing entry for the key is removed
    /// and disposed.
    /// </summary>
    public TItem Add<TItem>(TKey key, TItem item)
        where TItem : notnull
    {
        if (item is IDisposable disposable)
        {
            if (!_disposedValue)
            {
                IDisposable? old = null;
                if (_disposables.TryGetValue(key, out var existing) && !ReferenceEquals(existing, disposable))
                {
                    old = existing;
                }

                _disposables[key] = disposable;

                old?.Dispose();
            }
            else
            {
                disposable.Dispose();
            }
        }
        else
        {
            Remove(key);
        }

        return item;
    }

    public void Remove(TKey key)
    {
#if NET6_0_OR_GREATER
        if (_disposables.Remove(key, out var disposable))
        {
            disposable.Dispose();
        }
#else
        if (_disposables.TryGetValue(key, out var disposable))
        {
            _disposables.Remove(key);
            disposable.Dispose();
        }
#endif
    }

    public void Dispose()
    {
        if (!_disposedValue)
        {
            _disposedValue = true;
            List<Exception>? errors = null;
            foreach (var d in _disposables.Values)
            {
                try
                {
                    d.Dispose();
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add(ex);
                }
            }

            _disposables.Clear();

            if (errors is { Count: > 0 })
            {
                throw new AggregateException(errors);
            }
        }
    }
}
