// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

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
    /// <summary>
    /// The _innerCache field.
    /// </summary>
    private readonly ObservableCache<TObject, TKey> _innerCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntermediateCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <exception cref="ArgumentNullException">source.</exception>
    public IntermediateCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

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
    /// <param name="predicate">The predicate value.</param>
    /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        => _innerCache.Connect(predicate, suppressEmptyChangeSets);

    /// <inheritdoc />
    public void Dispose() => _innerCache.Dispose();

    /// <inheritdoc />
    /// <param name="updateAction">The updateAction value.</param>
    public void Edit(Action<ICacheUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromIntermediate(updateAction);

    /// <inheritdoc />
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

    /// <inheritdoc />
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null)
        => _innerCache.Preview(predicate);

    /// <inheritdoc />
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<Change<TObject, TKey>> Watch(TKey key) => _innerCache.Watch(key);

    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public IDisposable SuspendCount() => _innerCache.SuspendCount();

    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public IDisposable SuspendNotifications() => _innerCache.SuspendNotifications();

    /// <summary>
    /// Executes the GetInitialUpdates operation.
    /// </summary>
    /// <param name="filter">The filter value.</param>
    /// <returns>The result of the operation.</returns>
    internal IChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool>? filter = null) => _innerCache.GetInitialUpdates(filter);
}
