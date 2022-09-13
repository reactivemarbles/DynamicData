// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

[DebuggerDisplay("AnonymousObservableCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
internal sealed class AnonymousObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    where TKey : notnull
{
    private readonly IObservableCache<TObject, TKey> _cache;
    private readonly IDisposable _cleanUp;

    public AnonymousObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

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

    public IEnumerable<TObject> Items => _cache.Items;

    public IEnumerable<TKey> Keys => _cache.Keys;

    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        => _cache.Connect(predicate, suppressEmptyChangeSets);

    public Optional<TObject> Lookup(TKey key) => _cache.Lookup(key);

    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => _cache.Preview(predicate);

    public IObservable<Change<TObject, TKey>> Watch(TKey key) => _cache.Watch(key);

    public void Dispose() => _cleanUp.Dispose();
}
