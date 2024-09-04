// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using DynamicData.Binding;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

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
    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) => _innerCache.Connect(predicate, suppressEmptyChangeSets);

    /// <inheritdoc />
    public void Dispose() => _innerCache.Dispose();

    /// <inheritdoc />
    public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromSource(updateAction);

    /// <inheritdoc />
    public Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

    /// <inheritdoc />
    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => _innerCache.Preview(predicate);

    /// <inheritdoc />
    public IObservable<Change<TObject, TKey>> Watch(TKey key) => _innerCache.Watch(key);

    /// <inheritdoc />
    public IDisposable SuspendCount() => _innerCache.SuspendCount();

    /// <inheritdoc />
    public IDisposable SuspendNotifications() => _innerCache.SuspendNotifications();
}
