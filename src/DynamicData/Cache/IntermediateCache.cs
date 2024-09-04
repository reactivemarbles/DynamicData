// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using DynamicData.Binding;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Cache designed to be used for custom operator construction. It requires no key to be specified
/// but instead relies on the user specifying the key when amending data.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
[DebuggerDisplay("IntermediateCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
public sealed class IntermediateCache<TObject, TKey> : IIntermediateCache<TObject, TKey>, INotifyCollectionChangedSuspender
    where TObject : notnull
    where TKey : notnull
{
    private readonly ObservableCache<TObject, TKey> _innerCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntermediateCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public IntermediateCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        _innerCache = new ObservableCache<TObject, TKey>(source);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntermediateCache{TObject, TKey}"/> class.
    /// </summary>
    public IntermediateCache() => _innerCache = new ObservableCache<TObject, TKey>();

    /// <inheritdoc />
    public int Count => _innerCache.Count;

    /// <inheritdoc />
    public IObservable<int> CountChanged => _innerCache.CountChanged;

    /// <inheritdoc />
    public IReadOnlyList<TObject> Items => _innerCache.Items;

    /// <inheritdoc />
    public IReadOnlyList<TKey> Keys => _innerCache.Keys;

    /// <inheritdoc />
    public IReadOnlyDictionary<TKey, TObject> KeyValues => _innerCache.KeyValues;

    /// <inheritdoc />
    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        => _innerCache.Connect(predicate, suppressEmptyChangeSets);

    /// <inheritdoc />
    public void Dispose() => _innerCache.Dispose();

    /// <inheritdoc />
    public void Edit(Action<ICacheUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromIntermediate(updateAction);

    /// <inheritdoc />
    public Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

    /// <inheritdoc />
    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null)
        => _innerCache.Preview(predicate);

    /// <inheritdoc />
    public IObservable<Change<TObject, TKey>> Watch(TKey key) => _innerCache.Watch(key);

    /// <inheritdoc />
    public IDisposable SuspendCount() => _innerCache.SuspendCount();

    /// <inheritdoc />
    public IDisposable SuspendNotifications() => _innerCache.SuspendNotifications();

    internal IChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool>? filter = null) => _innerCache.GetInitialUpdates(filter);
}
