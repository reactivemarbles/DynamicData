// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

[DebuggerDisplay("AnonymousObservableCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
internal sealed class AnonymousObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed through _cleanUp")]
    private readonly IObservableCache<TObject, TKey> _cache;
    private readonly IDisposable _cleanUp;

    public AnonymousObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        _cache = new ObservableCache<TObject, TKey>(source);

        _cleanUp = _cache;
    }

    public AnonymousObservableCache(IObservableCache<TObject, TKey> cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        _cleanUp = Disposable.Empty;
    }

    public int Count => _cache.Count;

    public IObservable<int> CountChanged => _cache.CountChanged;

    public IReadOnlyList<TObject> Items => _cache.Items;

    public IReadOnlyList<TKey> Keys => _cache.Keys;

    public IReadOnlyDictionary<TKey, TObject> KeyValues => _cache.KeyValues;

    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        => _cache.Connect(predicate, suppressEmptyChangeSets);

    public Optional<TObject> Lookup(TKey key) => _cache.Lookup(key);

    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => _cache.Preview(predicate);

    public IObservable<Change<TObject, TKey>> Watch(TKey key) => _cache.Watch(key);

    public void Dispose() => _cleanUp.Dispose();
}
