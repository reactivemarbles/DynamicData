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
/// An observable cache which exposes an update API. Used at the root
/// of all observable chains.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
[DebuggerDisplay("LockFreeObservableCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
public sealed class LockFreeObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _changes field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Signal<IChangeSet<TObject, TKey>> _changes = new();

    /// <summary>
    /// The _changesPreview field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Signal<IChangeSet<TObject, TKey>> _changesPreview = new();

    /// <summary>
    /// The _countChanged field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Signal<int> _countChanged = new();

    /// <summary>
    /// The _cleanUp field.
    /// </summary>
    private readonly IDisposable _cleanUp;

    /// <summary>
    /// The _innerCache field.
    /// </summary>
    private readonly ChangeAwareCache<TObject, TKey> _innerCache = new();

    /// <summary>
    /// The _updater field.
    /// </summary>
    private readonly ICacheUpdater<TObject, TKey> _updater;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockFreeObservableCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public LockFreeObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        _updater = new CacheUpdater<TObject, TKey>(_innerCache);

        var loader = source.Select(
            changes =>
            {
                _innerCache.Clone(changes);
                return _innerCache.CaptureChanges();
            }).SubscribeSafe(_changes);

        _cleanUp = Disposable.Create(
            () =>
            {
                loader.Dispose();
                _changesPreview.OnCompleted();
                _changes.OnCompleted();
                _countChanged.OnCompleted();
            });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LockFreeObservableCache{TObject, TKey}"/> class.
    /// </summary>
    public LockFreeObservableCache()
    {
        _updater = new CacheUpdater<TObject, TKey>(_innerCache);

        _cleanUp = Disposable.Create(
            () =>
            {
                _changes.OnCompleted();
                _countChanged.OnCompleted();
            });
    }

    /// <inheritdoc />
    public int Count => _innerCache.Count;

    /// <inheritdoc />
    public IObservable<int> CountChanged => _countChanged.StartWith(_innerCache.Count).DistinctUntilChanged();

    /// <inheritdoc />
    public IReadOnlyList<TObject> Items => _innerCache.Items.ToArray();

    /// <inheritdoc />
    public IReadOnlyList<TKey> Keys => _innerCache.Keys.ToArray();

    /// <inheritdoc />
    public IReadOnlyDictionary<TKey, TObject> KeyValues => new Dictionary<TKey, TObject>(_innerCache.GetDictionary());

    /// <inheritdoc />
    /// <param name="predicate">The predicate value.</param>
    /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) => Observable.Defer(
            () =>
            {
                var initial = InternalEx.Return(() => _innerCache.GetInitialUpdates(predicate));
                var changes = initial.Concat(_changes);

                if (predicate != null)
                {
                    return changes.Filter(predicate, suppressEmptyChangeSets);
                }
                else if (suppressEmptyChangeSets)
                {
                    return changes.NotEmpty();
                }

                return changes;
            });

    /// <inheritdoc />
    public void Dispose() => _cleanUp.Dispose();

    /// <summary>
    /// Edits the specified edit action.
    /// </summary>
    /// <param name="editAction">The edit action.</param>
    public void Edit(Action<ICacheUpdater<TObject, TKey>> editAction)
    {
        ArgumentExceptionHelper.ThrowIfNull(editAction);

        editAction(_updater);
        _changes.OnNext(_innerCache.CaptureChanges());
    }

    /// <summary>
    /// Lookup a single item using the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The looked up value.</returns>
    /// <remarks>
    /// Fast indexed lookup.
    /// </remarks>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

    /// <inheritdoc />
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => predicate is null ? _changesPreview : _changesPreview.Filter(predicate);

    /// <summary>
    /// Returns an observable of any changes which match the specified key. The sequence starts with the initial item in the cache (if there is one).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>An observable that emits the changes.</returns>
    public IObservable<Change<TObject, TKey>> Watch(TKey key) => Observable.Create<Change<TObject, TKey>>(
            observer =>
            {
                var initial = _innerCache.Lookup(key);
                if (initial.HasValue)
                {
                    observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));
                }

                return _changes.Subscribe(
                    changes =>
                    {
                        foreach (var change in changes.ToConcreteType())
                        {
                            var match = EqualityComparer<TKey>.Default.Equals(change.Key, key);
                            if (match)
                            {
                                observer.OnNext(change);
                            }
                        }
                    });
            });
}
