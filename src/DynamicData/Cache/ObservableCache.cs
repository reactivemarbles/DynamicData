// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;
using DynamicData.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

[DebuggerDisplay("ObservableCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
internal sealed class ObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>, INotifyCollectionChangedSuspender
    where TObject : notnull
    where TKey : notnull
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<ChangeSet<TObject, TKey>> _changes = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<ChangeSet<TObject, TKey>> _changesPreview = new();

    private readonly IDisposable _cleanUp;

    private readonly Lazy<ISubject<int>> _countChanged = new(() => new Subject<int>());

    private readonly Lazy<SuspensionTracker> _suspensionTracker;

#if NET9_0_OR_GREATER
    private readonly Lock _locker = new();
#else
    private readonly object _locker = new();
#endif

    private readonly ReaderWriter<TObject, TKey> _readerWriter;

    private readonly DeliveryQueue<CacheUpdate> _notifications;

    private int _editLevel; // The level of recursion in editing.

    private long _currentVersion; // Monotonic counter incremented under lock for each enqueued change notification.

    private long _currentDeliveryVersion; // Version of the item currently being delivered. Set before _changes.OnNext.

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
                    notifications.Enqueue(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
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

    public ObservableCache(Func<TObject, TKey>? keySelector = null)
    {
        _readerWriter = new ReaderWriter<TObject, TKey>(keySelector);
        _notifications = new DeliveryQueue<CacheUpdate>(_locker, new CacheUpdateObserver(this));
        _suspensionTracker = new(() => new SuspensionTracker());

        _cleanUp = Disposable.Create(NotifyCompleted);
    }

    public int Count => _readerWriter.Count;

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

    public IReadOnlyList<TObject> Items => _readerWriter.Items;

    public IReadOnlyList<TKey> Keys => _readerWriter.Keys;

    public IReadOnlyDictionary<TKey, TObject> KeyValues => _readerWriter.KeyValues;

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

    public void Dispose() => _cleanUp.Dispose();

    public Optional<TObject> Lookup(TKey key) => _readerWriter.Lookup(key);

    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => predicate is null ? _changesPreview : _changesPreview.Filter(predicate);

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

    public IDisposable SuspendCount()
    {
        lock (_locker)
        {
            _suspensionTracker.Value.SuspendCount();
            return Disposable.Create(this, static cache => cache.ResumeCount());
        }
    }

    public IDisposable SuspendNotifications()
    {
        lock (_locker)
        {
            _suspensionTracker.Value.SuspendNotifications();
            return Disposable.Create(this, static cache => cache.ResumeNotifications());
        }
    }

    internal ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool>? filter = null) => _readerWriter.GetInitialUpdates(filter);

    internal void UpdateFromIntermediate(Action<ICacheUpdater<TObject, TKey>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

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
            notifications.Enqueue(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
        }
    }

    internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

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
            notifications.Enqueue(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
        }
    }

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
    private void InvokePreview(ChangeSet<TObject, TKey> changes)
    {
        if (changes.Count != 0 && !_notifications.IsTerminated)
        {
            _changesPreview.OnNext(changes);
        }
    }

    private void NotifyCompleted()
    {
        using var notifications = _notifications.AcquireLock();
        notifications.EnqueueCompleted();
    }

    private void NotifyError(Exception ex)
    {
        using var notifications = _notifications.AcquireLock();
        notifications.EnqueueError(ex);
    }

    /// <summary>
    /// Delivers a single notification to subscribers. This method is the delivery
    /// callback for <see cref="_notifications"/> and must never be called directly.
    /// It is invoked by the <see cref="DeliveryQueue{TItem}"/> after releasing the
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
            notifications.Enqueue(new CacheUpdate(null, _readerWriter.Count));
        }
    }

    private void ResumeNotifications()
    {
        bool emitResume;

        using (var notifications = _notifications.AcquireLock())
        {
            Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Notifications without Suspend Notifications instance");

            (var changes, emitResume) = _suspensionTracker.Value.ResumeNotifications();
            if (changes is not null)
            {
                notifications.Enqueue(new CacheUpdate(changes, _readerWriter.Count, ++_currentVersion));
            }
        }

        // Emit the resume signal after releasing the delivery scope so that
        // accumulated changes are delivered first
        if (emitResume)
        {
            using var readLock = _notifications.AcquireReadLock();
            _suspensionTracker.Value.EmitResumeNotification();
        }
    }

    /// <summary>
    /// The notification payload for cache delivery. Null Changes = count-only notification.
    /// </summary>
    private readonly record struct CacheUpdate(ChangeSet<TObject, TKey>? Changes, int Count, long Version = 0);

    /// <summary>
    /// Observer that dispatches <see cref="CacheUpdate"/> items to the cache's
    /// downstream subjects. Used as the delivery target for <see cref="_notifications"/>.
    /// </summary>
    private sealed class CacheUpdateObserver(ObservableCache<TObject, TKey> cache) : IObserver<CacheUpdate>
    {
        public void OnNext(CacheUpdate value)
        {
            if (value.Changes is not null)
            {
                Volatile.Write(ref cache._currentDeliveryVersion, value.Version);
                EmitChanges(value.Changes);
            }

            EmitCount(value.Count);
        }

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

    private sealed class SuspensionTracker : IDisposable
    {
        private readonly BehaviorSubject<bool> _areNotificationsSuspended = new(false);

        private List<Change<TObject, TKey>> _pendingChanges = [];

        private int _countSuspendCount;

        private int _notifySuspendCount;

        public bool IsCountSuspended => _countSuspendCount > 0;

        public bool AreNotificationsSuspended => _notifySuspendCount > 0;

        public IObservable<bool> NotificationsSuspendedObservable => _areNotificationsSuspended;

        public void EnqueueChanges(IEnumerable<Change<TObject, TKey>> changes)
        {
            Debug.Assert(changes is not null, "Don't pass in a null Enumerable");
            Debug.Assert(AreNotificationsSuspended, "Shouldn't be adding pending values if notifications aren't suspended");
            _pendingChanges.AddRange(changes);
        }

        public void SuspendNotifications()
        {
            if (++_notifySuspendCount == 1)
            {
                Debug.Assert(_pendingChanges.Count == 0, "Shouldn't be any pending values if suspend was just started");
                Debug.Assert(!_areNotificationsSuspended.Value, "SuspendSubject should be false for the first suspend call");
                _areNotificationsSuspended.OnNext(true);
            }
        }

        public void SuspendCount() => ++_countSuspendCount;

        public bool ResumeCount() => --_countSuspendCount == 0;

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

        public void EmitResumeNotification() => _areNotificationsSuspended.OnNext(false);

        public void Dispose()
        {
            _areNotificationsSuspended.OnCompleted();
            _areNotificationsSuspended.Dispose();
        }
    }
}
