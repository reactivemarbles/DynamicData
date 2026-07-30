// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Internal;
#else

namespace DynamicData.Internal;
#endif

/// <summary>
/// Manages Disposables by Key:
/// 1) Adding a disposable with the same key will dispose/replace the previous one.
/// 2) Adding when the container is Disposed will Dispose it immediately.
/// Thread-safe: all operations are internally synchronized.
/// </summary>
/// <typeparam name="TKey">Type to use for the Key.</typeparam>
internal sealed class KeyedDisposable<TKey> : IDisposable
    where TKey : notnull
{
    /// <summary>
    /// The _disposables field.
    /// </summary>
    private readonly Dictionary<TKey, IDisposable> _disposables = [];

    /// <summary>
    /// The _gate field.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// The _disposedValue field.
    /// </summary>
    private bool _disposedValue;

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
                return _disposables.Count;
        }
    }

    /// <summary>
    /// Gets the Keys value.
    /// </summary>
    public IEnumerable<TKey> Keys
    {
        get
        {
            lock (_gate)
                return _disposables.Keys.ToArray();
        }
    }

    /// <summary>
    /// Executes the ContainsKey operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public bool ContainsKey(TKey key)
    {
        lock (_gate)
            return _disposables.ContainsKey(key);
    }

    /// <summary>
    /// Gets the IsDisposed value.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_gate)
                return _disposedValue;
        }
    }

    /// <summary>
    /// Tracks an item by key. If the item implements <see cref="IDisposable"/>,
    /// it replaces any existing entry (disposing the previous one if different).
    /// If the item is NOT disposable, any existing entry for the key is removed
    /// and disposed.
    /// </summary>
    /// <typeparam name="TItem">The type of the TItem value.</typeparam>
    /// <param name="key">The key value.</param>
    /// <param name="item">The item value.</param>
    /// <returns>The result of the operation.</returns>
    public TItem Add<TItem>(TKey key, TItem item)
        where TItem : notnull
    {
        if (item is IDisposable disposable)
        {
            IDisposable? toDispose = null;

            lock (_gate)
            {
                if (!_disposedValue)
                {
                    if (_disposables.TryGetValue(key, out var existing))
                    {
                        if (ReferenceEquals(existing, disposable))
                        {
                            return item;
                        }

                        _disposables[key] = disposable;
                        toDispose = existing;
                    }
                    else
                    {
                        _disposables[key] = disposable;
                    }
                }
                else
                {
                    toDispose = disposable;
                }
            }

            toDispose?.Dispose();
        }
        else
        {
            Remove(key);
        }

        return item;
    }

    /// <summary>
    /// Executes the Remove operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    public void Remove(TKey key)
    {
        IDisposable? toDispose;
        lock (_gate)
        {
#if NET6_0_OR_GREATER
            if (!_disposables.Remove(key, out toDispose))
                return;
#else
            if (!_disposables.TryGetValue(key, out toDispose))
                return;
            _disposables.Remove(key);
#endif
        }

        toDispose.Dispose();
    }

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose()
    {
        Dictionary<TKey, IDisposable>? snapshot;
        lock (_gate)
        {
            if (_disposedValue)
                return;

            _disposedValue = true;
            snapshot = new Dictionary<TKey, IDisposable>(_disposables);
            _disposables.Clear();
        }

        List<Exception>? errors = null;
        foreach (var d in snapshot.Values)
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

        if (errors is { Count: > 0 })
        {
            throw new AggregateException(errors);
        }
    }
}
