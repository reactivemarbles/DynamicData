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
/// An observable cache which exposes an update API.  Used at the root
/// of all observable chains.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SourceCache{TObject, TKey}"/> class.
/// </remarks>
/// <param name="keySelector">The key selector.</param>
/// <exception cref="ArgumentNullException">keySelector.</exception>
[DebuggerDisplay("SourceCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
public sealed class SourceCache<TObject, TKey>(Func<TObject, TKey> keySelector) : ISourceCache<TObject, TKey>, INotifyCollectionChangedSuspender
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _innerCache field.
    /// </summary>
    private readonly ObservableCache<TObject, TKey> _innerCache = new(keySelector);

    /// <inheritdoc />
    public int Count => _innerCache.Count;

    /// <inheritdoc />
    public IObservable<int> CountChanged => _innerCache.CountChanged;

    /// <inheritdoc />
    public IReadOnlyList<TObject> Items => _innerCache.Items;

    /// <inheritdoc />
    public IReadOnlyList<TKey> Keys => _innerCache.Keys;

    /// <inheritdoc/>
    public Func<TObject, TKey> KeySelector { get; } = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

    /// <inheritdoc />
    public IReadOnlyDictionary<TKey, TObject> KeyValues => _innerCache.KeyValues;

    /// <inheritdoc />
    /// <param name="predicate">The predicate value.</param>
    /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) => _innerCache.Connect(predicate, suppressEmptyChangeSets);

    /// <inheritdoc />
    public void Dispose() => _innerCache.Dispose();

    /// <inheritdoc />
    /// <param name="updateAction">The updateAction value.</param>
    public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromSource(updateAction);

    /// <inheritdoc />
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

    /// <inheritdoc />
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => _innerCache.Preview(predicate);

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
}
