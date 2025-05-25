// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
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

    public TDisposable Add<TDisposable>(TKey key, TDisposable disposable)
        where TDisposable : IDisposable
    {
        disposable.ThrowArgumentNullExceptionIfNull(nameof(disposable));

        if (!_disposedValue)
        {
            Remove(key);
            _disposables.Add(key, disposable);
        }
        else
        {
            disposable.Dispose();
        }

        return disposable;
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
            disposable.Dispose();
            _disposables.Remove(key);
        }
#endif
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _disposedValue = true;
            if (disposing)
            {
                _disposables.Values.ForEach(d => d.Dispose());
                _disposables.Clear();
            }
        }
    }
}
