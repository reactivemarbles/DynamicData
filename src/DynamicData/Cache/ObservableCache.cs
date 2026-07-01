// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
#if REACTIVE_SHIM
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Provides members for the ObservableCache class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
[DebuggerDisplay("ObservableCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
internal sealed class ObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>, INotifyCollectionChangedSuspender
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _changes field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Signal<ChangeSet<TObject, TKey>> _changes = new();

    /// <summary>
    /// The _changesPreview field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Signal<ChangeSet<TObject, TKey>> _changesPreview = new();

    /// <summary>
    /// The _cleanUp field.
    /// </summary>
    private readonly IDisposable _cleanUp;

    /// <summary>
    /// The _countChanged field.
    /// </summary>
    private readonly Lazy<ISignal<int>> _countChanged = new(() => new Signal<int>());

    /// <summary>
    /// The _suspensionTracker field.
    /// </summary>
    private readonly Lazy<SuspensionTracker> _suspensionTracker;

    /// <summary>
    /// The _locker field.
    /// </summary>
    private readonly Lock _locker = new();

    /// <summary>
    /// The _readerWriter field.
    /// </summary>
    private readonly ReaderWriter<TObject, TKey> _readerWriter;

    /// <summary>
    /// The _notifications field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Terminated via NotifyCompleted in _cleanUp")]
    private readonly DeliveryQueue<CacheUpdate> _notifications;

    /// <summary>
    /// The _editLevel field.
    /// </summary>
    private int _editLevel; // The level of recursion in editing.

    /// <summary>
    /// The _currentVersion field.
    /// </summary>
    private long _currentVersion; // Monotonic counter incremented under lock for each enqueued change notification.

    /// <summary>
    /// The _currentDeliveryVersion field.
    /// </summary>
    private long _currentDeliveryVersion; // Version of the item currently being delivered. Set before _changes.OnNext.

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        _readerWriter = new ReaderWriter<TObject, TKey>();
        _notifications = new DeliveryQueue<CacheUpdate>(_locker, new CacheUpdateObserver(this));
        _suspensionTracker = new(() => new SuspensionTracker());

        var loader = source.Subscribe(
            changeSet =>
            {
                using var notifications = _notifications.AcquireLock();

                var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                var changes = _readerWriter.Write(changeSet, previewHandler, _changes.HasObservers);

                if (changes is not null)
                {
                    notifications.EnqueueNext(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
                }
            },
            NotifyError,
            NotifyCompleted);

        _cleanUp = Disposable.Create(
            () =>
            {
                loader.Dispose();
                NotifyCompleted();
            });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="keySelector">The keySelector value.</param>
    public ObservableCache(Func<TObject, TKey>? keySelector = null)
    {
        _readerWriter = new ReaderWriter<TObject, TKey>(keySelector);
        _notifications = new DeliveryQueue<CacheUpdate>(_locker, new CacheUpdateObserver(this));
        _suspensionTracker = new(() => new SuspensionTracker());

        _cleanUp = Disposable.Create(NotifyCompleted);
    }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _readerWriter.Count;

    /// <summary>
    /// Gets the CountChanged value.
    /// </summary>
    public IObservable<int> CountChanged =>
        Observable.Create<int>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                var snapshotVersion = _currentVersion;
                var countChanged = readLock.HasPending
                    ? _countChanged.Value.SkipWhile(_ => Volatile.Read(ref _currentDeliveryVersion) <= snapshotVersion)
                    : _countChanged.Value;

                var source = countChanged.StartWith(_readerWriter.Count).DistinctUntilChanged();
                return source.SubscribeSafe(observer);
            });

    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IReadOnlyList<TObject> Items => _readerWriter.Items;

    /// <summary>
    /// Gets the Keys value.
    /// </summary>
    public IReadOnlyList<TKey> Keys => _readerWriter.Keys;

    /// <summary>
    /// Gets the KeyValues value.
    /// </summary>
    public IReadOnlyDictionary<TKey, TObject> KeyValues => _readerWriter.KeyValues;

    /// <summary>
    /// Executes the Connect operation.
    /// </summary>
    /// <param name="predicate">The predicate value.</param>
    /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) =>
        Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            lock (_locker)
            {
                var observable = (!_suspensionTracker.IsValueCreated || !_suspensionTracker.Value.AreNotificationsSuspended)

                    // Create the Connection Observable
                    ? CreateConnectObservable(predicate, suppressEmptyChangeSets)

                    // Defer until notifications are no longer suspended
                    : _suspensionTracker.Value.NotificationsSuspendedObservable.Do(static _ => { }, observer.OnCompleted)
                        .Where(static b => !b).Take(1).Select(_ => CreateConnectObservable(predicate, suppressEmptyChangeSets)).Switch();

                return observable.SubscribeSafe(observer);
            }
        });

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose() => _cleanUp.Dispose();

    /// <summary>
    /// Executes the Lookup operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key) => _readerWriter.Lookup(key);

    /// <summary>
    /// Executes the Preview operation.
    /// </summary>
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => predicate is null ? _changesPreview : _changesPreview.Filter(predicate);

    /// <summary>
    /// Executes the Watch operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<Change<TObject, TKey>> Watch(TKey key) =>
        Observable.Create<Change<TObject, TKey>>(observer =>
        {
            lock (_locker)
            {
                var observable = (!_suspensionTracker.IsValueCreated || !_suspensionTracker.Value.AreNotificationsSuspended)

                    // Create the Watch Observable
                    ? CreateWatchObservable(key)

                    // Defer until notifications are no longer suspended
                    : _suspensionTracker.Value.NotificationsSuspendedObservable.Do(static _ => { }, observer.OnCompleted)
                        .Where(static b => !b).Take(1).Select(_ => CreateWatchObservable(key)).Switch();

                return observable.SubscribeSafe(observer);
            }
        });

    /// <summary>
    /// Executes the SuspendCount operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IDisposable SuspendCount()
    {
        lock (_locker)
        {
            _suspensionTracker.Value.SuspendCount();
            return Disposable.Create(this, static cache => cache.ResumeCount());
        }
    }

    /// <summary>
    /// Executes the SuspendNotifications operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IDisposable SuspendNotifications()
    {
        lock (_locker)
        {
            _suspensionTracker.Value.SuspendNotifications();
            return Disposable.Create(this, static cache => cache.ResumeNotifications());
        }
    }

    /// <summary>
    /// Executes the GetInitialUpdates operation.
    /// </summary>
    /// <param name="filter">The filter value.</param>
    /// <returns>The result of the operation.</returns>
    internal ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool>? filter = null) => _readerWriter.GetInitialUpdates(filter);

    /// <summary>
    /// Executes the UpdateFromIntermediate operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    internal void UpdateFromIntermediate(Action<ICacheUpdater<TObject, TKey>> updateAction)
    {
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

        using var notifications = _notifications.AcquireLock();

        ChangeSet<TObject, TKey>? changes = null;

        _editLevel++;
        if (_editLevel == 1)
        {
            var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
            changes = _readerWriter.Write(updateAction, previewHandler, _changes.HasObservers);
        }
        else
        {
            _readerWriter.WriteNested(updateAction);
        }

        _editLevel--;

        if (changes is not null && _editLevel == 0)
        {
            notifications.EnqueueNext(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
        }
    }

    /// <summary>
    /// Executes the UpdateFromSource operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
    {
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

        using var notifications = _notifications.AcquireLock();

        ChangeSet<TObject, TKey>? changes = null;

        _editLevel++;
        if (_editLevel == 1)
        {
            var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
            changes = _readerWriter.Write(updateAction, previewHandler, _changes.HasObservers);
        }
        else
        {
            _readerWriter.WriteNested(updateAction);
        }

        _editLevel--;

        if (changes is not null && _editLevel == 0)
        {
            notifications.EnqueueNext(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
        }
    }

    /// <summary>
    /// Executes the CreateConnectObservable operation.
    /// </summary>
    /// <param name="predicate">The predicate value.</param>
    /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TObject, TKey>> CreateConnectObservable(Func<TObject, bool>? predicate, bool suppressEmptyChangeSets) =>
        Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                var initial = InternalEx.Return(() => (IChangeSet<TObject, TKey>)GetInitialUpdates(predicate));

                // The current snapshot may contain changes that have been made but the notifications
                // have yet to be delivered.  We need to filter those out to avoid delivering an update
                // that has already been applied (but detect this possibility and skip filtering unless absolutely needed)
                var snapshotVersion = _currentVersion;
                var changes = readLock.HasPending
                    ? _changes.SkipWhile(_ => Volatile.Read(ref _currentDeliveryVersion) <= snapshotVersion)
                    : (IObservable<IChangeSet<TObject, TKey>>)_changes;

                changes = initial.Concat(changes);

                if (predicate != null)
                {
                    changes = changes.Filter(predicate, suppressEmptyChangeSets);
                }
                else if (suppressEmptyChangeSets)
                {
                    changes = changes.NotEmpty();
                }

                return changes.SubscribeSafe(observer);
            });

    /// <summary>
    /// Executes the CreateWatchObservable operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    private IObservable<Change<TObject, TKey>> CreateWatchObservable(TKey key) =>
        Observable.Create<Change<TObject, TKey>>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                var initial = _readerWriter.Lookup(key);
                if (initial.HasValue)
                {
                    observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));
                }

                // The current snapshot may contain changes that have been made but the notifications
                // have yet to be delivered.  We need to filter those out to avoid delivering an update
                // that has already been applied (but detect this possibility and skip filtering unless absolutely needed)
                var snapshotVersion = _currentVersion;
                var changes = readLock.HasPending
                    ? _changes.SkipWhile(_ => Volatile.Read(ref _currentDeliveryVersion) <= snapshotVersion)
                    : _changes;

                return changes.Finally(observer.OnCompleted).Subscribe(
                    changes =>
                    {
                        foreach (var change in changes)
                        {
                            var match = EqualityComparer<TKey>.Default.Equals(change.Key, key);
                            if (match)
                            {
                                observer.OnNext(change);
                            }
                        }
                    });
            });

    /// <summary>
    /// Delivers a preview notification synchronously under _locker. Preview is
    /// called by ReaderWriter during a write, between two data swaps, so it MUST
    /// fire under the lock with the pre-write state visible to subscribers.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    private void InvokePreview(ChangeSet<TObject, TKey> changes)
    {
        if (changes.Count != 0 && !_notifications.IsTerminated)
        {
            _changesPreview.OnNext(changes);
        }
    }

    /// <summary>
    /// Executes the NotifyCompleted operation.
    /// </summary>
    private void NotifyCompleted()
    {
        using var notifications = _notifications.AcquireLock();
        notifications.EnqueueCompleted();
    }

    /// <summary>
    /// Executes the NotifyError operation.
    /// </summary>
    /// <param name="ex">The ex value.</param>
    private void NotifyError(Exception ex)
    {
        using var notifications = _notifications.AcquireLock();
        notifications.EnqueueError(ex);
    }

    /// <summary>
    /// Delivers a single notification to subscribers. This method is the delivery
    /// callback for <see cref="_notifications"/> and must never be called directly.
    /// It is invoked by the <c>DeliveryQueue&lt;TItem&gt;</c> after releasing the
    /// lock, which guarantees that no lock is held when subscriber code runs. The
    /// queue's single-deliverer token ensures this method is never called concurrently,
    /// preserving the Rx serialization contract across all subjects.
    /// Returns true to continue delivery, or false for terminal items (OnCompleted/OnError)
    /// which causes the queue to self-terminate.
    /// </summary>
    private void ResumeCount()
    {
        using var notifications = _notifications.AcquireLock();
        Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Count without Suspend Count instance");

        if (_suspensionTracker.Value.ResumeCount() && _countChanged.IsValueCreated)
        {
            notifications.EnqueueNext(new CacheUpdate(null, _readerWriter.Count));
        }
    }

    /// <summary>
    /// Executes the ResumeNotifications operation.
    /// </summary>
    private void ResumeNotifications()
    {
        using var notifications = _notifications.AcquireLock();
        Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Notifications without Suspend Notifications instance");

        var (changes, emitResume) = _suspensionTracker.Value.ResumeNotifications();
        if (changes is not null)
        {
            notifications.EnqueueNext(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
        }

        if (emitResume)
        {
            _suspensionTracker.Value.EmitResumeNotification();
        }
    }

    /// <summary>
    /// The notification payload for cache delivery. Null Changes = count-only notification.
    /// </summary>
    /// <param name="Changes">The Changes value.</param>
    /// <param name="Count">The Count value.</param>
    /// <param name="Version">The Version value.</param>
    private readonly record struct CacheUpdate(ChangeSet<TObject, TKey>? Changes, int Count, long Version = 0);

/// <summary>
/// Observer that dispatches <see cref="CacheUpdate"/> items to the cache's
/// downstream subjects. Used as the delivery target for <see cref="_notifications"/>.
/// </summary>
/// <param name="cache">The cache value.</param>
private sealed class CacheUpdateObserver(ObservableCache<TObject, TKey> cache) : IObserver<CacheUpdate>
    {
        /// <summary>
        /// Executes the OnNext operation.
        /// </summary>
        /// <param name="value">The value value.</param>
        public void OnNext(CacheUpdate value)
        {
            if (value.Changes is not null)
            {
                Volatile.Write(ref cache._currentDeliveryVersion, value.Version);
                EmitChanges(value.Changes);
            }

            EmitCount(value.Count);
        }

        /// <summary>
        /// Executes the OnError operation.
        /// </summary>
        /// <param name="error">The error value.</param>
        public void OnError(Exception error)
        {
            cache._changesPreview.OnError(error);
            cache._changes.OnError(error);

            if (cache._countChanged.IsValueCreated)
            {
                cache._countChanged.Value.OnError(error);
            }

            if (cache._suspensionTracker.IsValueCreated)
            {
                cache._suspensionTracker.Value.Dispose();
            }
        }

        /// <summary>
        /// Executes the OnCompleted operation.
        /// </summary>
        public void OnCompleted()
        {
            cache._changes.OnCompleted();
            cache._changesPreview.OnCompleted();

            if (cache._countChanged.IsValueCreated)
            {
                cache._countChanged.Value.OnCompleted();
            }

            if (cache._suspensionTracker.IsValueCreated)
            {
                cache._suspensionTracker.Value.Dispose();
            }
        }

        /// <summary>
        /// Executes the EmitChanges operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        private void EmitChanges(ChangeSet<TObject, TKey> changes)
        {
            if (cache._suspensionTracker.IsValueCreated)
            {
                lock (cache._locker)
                {
                    if (cache._suspensionTracker.Value.AreNotificationsSuspended)
                    {
                        cache._suspensionTracker.Value.EnqueueChanges(changes);
                        return;
                    }
                }
            }

            cache._changes.OnNext(changes);
        }

        /// <summary>
        /// Executes the EmitCount operation.
        /// </summary>
        /// <param name="count">The count value.</param>
        private void EmitCount(int count)
        {
            if (cache._suspensionTracker.IsValueCreated)
            {
                lock (cache._locker)
                {
                    if (cache._suspensionTracker.Value.IsCountSuspended)
                    {
                        return;
                    }
                }
            }

            if (cache._countChanged.IsValueCreated)
            {
                cache._countChanged.Value.OnNext(count);
            }
        }
    }

/// <summary>
/// Provides members for the SuspensionTracker class.
/// </summary>
private sealed class SuspensionTracker : IDisposable
    {
        /// <summary>
        /// The _areNotificationsSuspended field.
        /// </summary>
        private readonly BehaviorSignal<bool> _areNotificationsSuspended = new(false);

        /// <summary>
        /// The _pendingChanges field.
        /// </summary>
        private List<Change<TObject, TKey>> _pendingChanges = [];

        /// <summary>
        /// The _countSuspendCount field.
        /// </summary>
        private int _countSuspendCount;

        /// <summary>
        /// The _notifySuspendCount field.
        /// </summary>
        private int _notifySuspendCount;

        /// <summary>
        /// Gets the IsCountSuspended value.
        /// </summary>
        public bool IsCountSuspended => _countSuspendCount > 0;

        /// <summary>
        /// Gets the AreNotificationsSuspended value.
        /// </summary>
        public bool AreNotificationsSuspended => _notifySuspendCount > 0;

        /// <summary>
        /// Gets the NotificationsSuspendedObservable value.
        /// </summary>
        public IObservable<bool> NotificationsSuspendedObservable => _areNotificationsSuspended;

        /// <summary>
        /// Executes the EnqueueChanges operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        public void EnqueueChanges(IEnumerable<Change<TObject, TKey>> changes)
        {
            Debug.Assert(changes is not null, "Don't pass in a null Enumerable");
            Debug.Assert(AreNotificationsSuspended, "Shouldn't be adding pending values if notifications aren't suspended");
            _pendingChanges.AddRange(changes);
        }

        /// <summary>
        /// Executes the SuspendNotifications operation.
        /// </summary>
        public void SuspendNotifications()
        {
            if (++_notifySuspendCount == 1)
            {
                Debug.Assert(_pendingChanges.Count == 0, "Shouldn't be any pending values if suspend was just started");
                Debug.Assert(!_areNotificationsSuspended.Value, "SuspendSubject should be false for the first suspend call");
                _areNotificationsSuspended.OnNext(true);
            }
        }

        /// <summary>
        /// Executes the SuspendCount operation.
        /// </summary>
        public void SuspendCount() => ++_countSuspendCount;

        /// <summary>
        /// Executes the ResumeCount operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public bool ResumeCount() => --_countSuspendCount == 0;

        /// <summary>
        /// Executes the ResumeNotifications operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public (ChangeSet<TObject, TKey>? Changes, bool EmitResume) ResumeNotifications()
        {
            if (--_notifySuspendCount == 0 && !_areNotificationsSuspended.IsDisposed)
            {
                ChangeSet<TObject, TKey>? changes = null;

                if (_pendingChanges.Count > 0)
                {
                    var changesToDeliver = _pendingChanges;
                    _pendingChanges = [];
                    changes = new ChangeSet<TObject, TKey>(changesToDeliver);
                }

                return (changes, true);
            }

            return (null, false);
        }

        /// <summary>
        /// Executes the EmitResumeNotification operation.
        /// </summary>
        public void EmitResumeNotification() => _areNotificationsSuspended.OnNext(false);

        /// <summary>
        /// Executes the Dispose operation.
        /// </summary>
        public void Dispose()
        {
            _areNotificationsSuspended.OnCompleted();
            _areNotificationsSuspended.Dispose();
        }
    }
}
