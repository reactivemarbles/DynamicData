// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the AnonymousObservableCache class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
[DebuggerDisplay("AnonymousObservableCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
internal sealed class AnonymousObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed through _cleanUp")]
    private readonly IObservableCache<TObject, TKey> _cache;

    /// <summary>
    /// The _cleanUp field.
    /// </summary>
    private readonly IDisposable _cleanUp;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousObservableCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    public AnonymousObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        _cache = new ObservableCache<TObject, TKey>(source);

        _cleanUp = _cache;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousObservableCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    public AnonymousObservableCache(IObservableCache<TObject, TKey> cache)
    {
        ArgumentExceptionHelper.ThrowIfNull(cache);

        _cache = cache;
        _cleanUp = Disposable.Empty;
    }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets the CountChanged value.
    /// </summary>
    public IObservable<int> CountChanged => _cache.CountChanged;

    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IReadOnlyList<TObject> Items => _cache.Items;

    /// <summary>
    /// Gets the Keys value.
    /// </summary>
    public IReadOnlyList<TKey> Keys => _cache.Keys;

    /// <summary>
    /// Gets the KeyValues value.
    /// </summary>
    public IReadOnlyDictionary<TKey, TObject> KeyValues => _cache.KeyValues;

    /// <summary>
    /// Executes the Connect operation.
    /// </summary>
    /// <param name="predicate">The predicate value.</param>
    /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
        => _cache.Connect(predicate, suppressEmptyChangeSets);

    /// <summary>
    /// Executes the Lookup operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key) => _cache.Lookup(key);

    /// <summary>
    /// Executes the Preview operation.
    /// </summary>
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => _cache.Preview(predicate);

    /// <summary>
    /// Executes the Watch operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<Change<TObject, TKey>> Watch(TKey key) => _cache.Watch(key);

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose() => _cleanUp.Dispose();
}
