// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<IChangeSet<TObject, TKey>> _changes = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<IChangeSet<TObject, TKey>> _changesPreview = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<int> _countChanged = new();

    private readonly IDisposable _cleanUp;

    private readonly ChangeAwareCache<TObject, TKey> _innerCache = new();

    private readonly ICacheUpdater<TObject, TKey> _updater;

    /// <summary>
    /// Initializes a new instance of the <see cref="LockFreeObservableCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public LockFreeObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
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
        if (editAction is null)
        {
            throw new ArgumentNullException(nameof(editAction));
        }

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
    public Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

    /// <inheritdoc />
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
