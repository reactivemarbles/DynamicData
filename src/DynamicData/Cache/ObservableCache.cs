// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

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

    private readonly Queue<NotificationItem> _notificationQueue = new();

    private int _editLevel; // The level of recursion in editing.

    private bool _isDraining;

    // Set under _locker when terminal events are delivered or Dispose runs.
    // Checked by DeliverNotification to skip delivery after termination.
    // Volatile because it's read outside _locker in DrainOutsideLock's delivery path.
    private volatile bool _isTerminated;

    // Tracks how many items currently in the queue will produce _changes.OnNext.
    // Excludes suspended, count-only, and terminal items. Incremented at enqueue,
    // decremented at dequeue (both under _locker). Used by Connect/Watch for
    // precise Skip(N) that avoids both duplicates and missed notifications.
    private int _pendingChangesOnNextCount;

    public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        _readerWriter = new ReaderWriter<TObject, TKey>();
        _suspensionTracker = new(() => new SuspensionTracker(EnqueueChanges, EnqueueCount));

        var loader = source.Subscribe(
            changeSet =>
            {
                bool shouldDrain;
                lock (_locker)
                {
                    var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                    var changes = _readerWriter.Write(changeSet, previewHandler, _changes.HasObservers);

                    if (changes is null)
                    {
                        return;
                    }

                    EnqueueUnderLock(changes);
                    shouldDrain = TryStartDrain();
                }

                if (shouldDrain)
                {
                    DrainOutsideLock();
                }
            },
            ex =>
            {
                bool shouldDrain;
                lock (_locker)
                {
                    _notificationQueue.Enqueue(NotificationItem.CreateError(ex));
                    shouldDrain = TryStartDrain();
                }

                if (shouldDrain)
                {
                    DrainOutsideLock();
                }
            },
            () =>
            {
                bool shouldDrain;
                lock (_locker)
                {
                    _notificationQueue.Enqueue(NotificationItem.CreateCompleted());
                    shouldDrain = TryStartDrain();
                }

                if (shouldDrain)
                {
                    DrainOutsideLock();
                }
            });

        _cleanUp = Disposable.Create(
            () =>
            {
                loader.Dispose();

                lock (_locker)
                {
                    // Dispose is a teardown path. Clear pending items and terminate directly.
                    _isTerminated = true;
                    _pendingChangesOnNextCount = 0;
                    _notificationQueue.Clear();
                    _changes.OnCompleted();
                    _changesPreview.OnCompleted();

                    if (_countChanged.IsValueCreated)
                    {
                        _countChanged.Value.OnCompleted();
                    }

                    if (_suspensionTracker.IsValueCreated)
                    {
                        _suspensionTracker.Value.Dispose();
                    }
                }
            });
    }

    public ObservableCache(Func<TObject, TKey>? keySelector = null)
    {
        _readerWriter = new ReaderWriter<TObject, TKey>(keySelector);
        _suspensionTracker = new(() => new SuspensionTracker(EnqueueChanges, EnqueueCount));

        _cleanUp = Disposable.Create(
            () =>
            {
                lock (_locker)
                {
                    _isTerminated = true;
                    _pendingChangesOnNextCount = 0;
                    _notificationQueue.Clear();
                    _changes.OnCompleted();
                    _changesPreview.OnCompleted();

                    if (_countChanged.IsValueCreated)
                    {
                        _countChanged.Value.OnCompleted();
                    }

                    if (_suspensionTracker.IsValueCreated)
                    {
                        _suspensionTracker.Value.Dispose();
                    }
                }
            });
    }

    public int Count => _readerWriter.Count;

    public IObservable<int> CountChanged =>
        Observable.Create<int>(
            observer =>
            {
                lock (_locker)
                {
                    var skipCount = _notificationQueue.Count;
                    var countStream = skipCount > 0 ? _countChanged.Value.Skip(skipCount) : _countChanged.Value;
                    var source = countStream.StartWith(_readerWriter.Count).DistinctUntilChanged();
                    return source.SubscribeSafe(observer);
                }
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

        bool shouldDrain;
        lock (_locker)
        {
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
                EnqueueUnderLock(changes);
            }

            shouldDrain = TryStartDrain();
        }

        if (shouldDrain)
        {
            DrainOutsideLock();
        }
    }

    internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        bool shouldDrain;
        lock (_locker)
        {
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
                EnqueueUnderLock(changes);
            }

            shouldDrain = TryStartDrain();
        }

        if (shouldDrain)
        {
            DrainOutsideLock();
        }
    }

    private IObservable<IChangeSet<TObject, TKey>> CreateConnectObservable(Func<TObject, bool>? predicate, bool suppressEmptyChangeSets) =>
        Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                lock (_locker)
                {
                    // Skip pending notifications that will emit to _changes.OnNext.
                    // Uses a precise counter (excludes suspended, count-only, and terminal items)
                    // to avoid over-skipping future legitimate notifications.
                    var skipCount = _pendingChangesOnNextCount;

                    var initial = InternalEx.Return(() => (IChangeSet<TObject, TKey>)GetInitialUpdates(predicate));
                    var changesStream = skipCount > 0 ? _changes.Skip(skipCount) : _changes;
                    var changes = initial.Concat(changesStream);

                    if (predicate != null)
                    {
                        changes = changes.Filter(predicate, suppressEmptyChangeSets);
                    }
                    else if (suppressEmptyChangeSets)
                    {
                        changes = changes.NotEmpty();
                    }

                    return changes.SubscribeSafe(observer);
                }
            });

    private IObservable<Change<TObject, TKey>> CreateWatchObservable(TKey key) =>
        Observable.Create<Change<TObject, TKey>>(
            observer =>
            {
                lock (_locker)
                {
                    var skipCount = _pendingChangesOnNextCount;

                    var initial = _readerWriter.Lookup(key);
                    if (initial.HasValue)
                    {
                        observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));
                    }

                    var changesStream = skipCount > 0 ? _changes.Skip(skipCount) : _changes;
                    return changesStream.Finally(observer.OnCompleted).Subscribe(
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
                }
            });

    /// <summary>
    /// Delivers a preview notification synchronously under _locker. Preview is
    /// called by ReaderWriter during a write, between two data swaps, so it MUST
    /// fire under the lock with the pre-write state visible to subscribers.
    /// </summary>
    private void InvokePreview(ChangeSet<TObject, TKey> changes)
    {
        if (changes.Count != 0)
        {
            _changesPreview.OnNext(changes);
        }
    }

    /// <summary>
    /// Enqueues a changeset (plus associated count) for delivery outside the lock.
    /// Must be called while _locker is held.
    /// </summary>
    private void EnqueueUnderLock(ChangeSet<TObject, TKey> changes)
    {
        // Check suspension state under lock to avoid TOCTOU race.
        var isSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.AreNotificationsSuspended;
        var isCountSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.IsCountSuspended;

        _notificationQueue.Enqueue(new NotificationItem(changes, _readerWriter.Count, isSuspended, isCountSuspended));

        if (!isSuspended)
        {
            _pendingChangesOnNextCount++;
        }
    }

    /// <summary>
    /// Attempts to claim the drain token. Returns true if this thread should drain.
    /// Must be called while _locker is held.
    /// </summary>
    private bool TryStartDrain()
    {
        if (_isDraining || _notificationQueue.Count == 0)
        {
            return false;
        }

        _isDraining = true;
        return true;
    }

    /// <summary>
    /// Delivers all pending notifications outside the lock. Only the thread that
    /// successfully called TryStartDrain may call this. Serializes all OnNext
    /// calls for this cache instance, preserving the Rx contract.
    /// </summary>
    private void DrainOutsideLock()
    {
        try
        {
            while (true)
            {
                NotificationItem item;
                lock (_locker)
                {
                    if (_notificationQueue.Count == 0)
                    {
                        _isDraining = false;
                        return;
                    }

                    item = _notificationQueue.Dequeue();

                    // Decrement the per-subject counter for items that will emit _changes.OnNext.
                    if (!item.IsSuspended && !item.IsCountOnly && !item.IsCompleted && !item.IsError)
                    {
                        _pendingChangesOnNextCount--;
                    }
                }

                DeliverNotification(item);
            }
        }
        catch
        {
            lock (_locker)
            {
                _isDraining = false;
                _pendingChangesOnNextCount = 0;
            }

            throw;
        }
    }

    private void DeliverNotification(NotificationItem item)
    {
        // After Dispose or a terminal event has been delivered, skip all delivery.
        // Subject.OnNext after OnCompleted is a no-op, but this avoids wasted work
        // and prevents subtle ordering issues.
        if (_isTerminated)
        {
            return;
        }

        if (item.IsCompleted)
        {
            _isTerminated = true;
            _changes.OnCompleted();
            _changesPreview.OnCompleted();

            if (_countChanged.IsValueCreated)
            {
                _countChanged.Value.OnCompleted();
            }

            return;
        }

        if (item.IsError)
        {
            _isTerminated = true;
            _changesPreview.OnError(item.Error!);
            _changes.OnError(item.Error!);
            return;
        }

        if (item.IsCountOnly)
        {
            if (_countChanged.IsValueCreated)
            {
                _countChanged.Value.OnNext(item.Count);
            }

            return;
        }

        // Suspension state was captured at enqueue time (under lock) to avoid TOCTOU.
        // For unsuspended items, deliver directly. For suspended items, re-check the
        // live state under lock — ResumeNotifications may have run between dequeue and
        // delivery, in which case we deliver directly instead of orphaning in _pendingChanges.
        if (!item.IsSuspended)
        {
            _changes.OnNext(item.Changes);
        }
        else
        {
            bool deliverNow;
            lock (_locker)
            {
                if (_suspensionTracker.Value.AreNotificationsSuspended)
                {
                    _suspensionTracker.Value.EnqueueChanges(item.Changes);
                    deliverNow = false;
                }
                else
                {
                    deliverNow = true;
                }
            }

            if (deliverNow)
            {
                _changes.OnNext(item.Changes);
            }
        }

        if (!item.IsCountSuspended)
        {
            if (_countChanged.IsValueCreated)
            {
                _countChanged.Value.OnNext(item.Count);
            }
        }
    }

    /// <summary>
    /// Called by SuspensionTracker.ResumeNotifications to deliver accumulated
    /// changes. This enqueues under _locker; the caller's TryStartDrain +
    /// DrainOutsideLock handles delivery outside the lock.
    /// </summary>
    private void EnqueueChanges(ChangeSet<TObject, TKey> changes)
    {
        _notificationQueue.Enqueue(new NotificationItem(changes, _readerWriter.Count, isSuspended: false, isCountSuspended: false));
        _pendingChangesOnNextCount++;
    }

    /// <summary>
    /// Called by SuspensionTracker.ResumeCount to deliver the current count.
    /// </summary>
    private void EnqueueCount()
    {
        if (_countChanged.IsValueCreated)
        {
            _notificationQueue.Enqueue(NotificationItem.CreateCountOnly(_readerWriter.Count));
        }
    }

    private void ResumeCount()
    {
        bool shouldDrain;
        lock (_locker)
        {
            Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Count without Suspend Count instance");
            _suspensionTracker.Value.ResumeCount();
            shouldDrain = TryStartDrain();
        }

        if (shouldDrain)
        {
            DrainOutsideLock();
        }
    }

    private void ResumeNotifications()
    {
        bool shouldDrain;
        lock (_locker)
        {
            Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Notifications without Suspend Notifications instance");
            _suspensionTracker.Value.ResumeNotifications();
            shouldDrain = TryStartDrain();
        }

        if (shouldDrain)
        {
            DrainOutsideLock();
        }
    }

    private readonly record struct NotificationItem
    {
        public ChangeSet<TObject, TKey> Changes { get; }

        public int Count { get; }

        public bool IsCountOnly { get; }

        public bool IsSuspended { get; }

        public bool IsCountSuspended { get; }

        public bool IsCompleted { get; }

        public bool IsError { get; }

        public Exception? Error { get; }

        public NotificationItem(ChangeSet<TObject, TKey> changes, int count, bool isSuspended, bool isCountSuspended)
        {
            Changes = changes;
            Count = count;
            IsSuspended = isSuspended;
            IsCountSuspended = isCountSuspended;
        }

        private NotificationItem(int count, bool isCountOnly)
        {
            Changes = [];
            Count = count;
            IsCountOnly = isCountOnly;
        }

        private NotificationItem(bool isCompleted, Exception? error)
        {
            Changes = [];
            IsCompleted = isCompleted;
            IsError = error is not null;
            Error = error;
        }

        public static NotificationItem CreateCountOnly(int count) => new(count, isCountOnly: true);

        public static NotificationItem CreateCompleted() => new(isCompleted: true, error: null);

        public static NotificationItem CreateError(Exception error) => new(isCompleted: false, error: error);
    }

    private sealed class SuspensionTracker(Action<ChangeSet<TObject, TKey>> onResumeNotifications, Action onResumeCount) : IDisposable
    {
        private readonly BehaviorSubject<bool> _areNotificationsSuspended = new(false);

        private readonly Action<ChangeSet<TObject, TKey>> _onResumeNotifications = onResumeNotifications;

        private readonly Action _onResumeCount = onResumeCount;

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

        public void ResumeNotifications()
        {
            if (--_notifySuspendCount == 0 && !_areNotificationsSuspended.IsDisposed)
            {
                // Swap out pending changes before the callback to handle re-entrant
                // suspend/resume correctly. If a subscriber re-suspends during the
                // callback, new changes go into the fresh list, not the one being delivered.
                if (_pendingChanges.Count > 0)
                {
                    var changesToDeliver = _pendingChanges;
                    _pendingChanges = [];
                    _onResumeNotifications(new ChangeSet<TObject, TKey>(changesToDeliver));
                }

                // Re-check: a subscriber callback may have re-suspended during delivery.
                if (_notifySuspendCount == 0)
                {
                    _areNotificationsSuspended.OnNext(false);
                }
            }
        }

        public void ResumeCount()
        {
            if (--_countSuspendCount == 0)
            {
                _onResumeCount();
            }
        }

        public void Dispose()
        {
            _areNotificationsSuspended.OnCompleted();
            _areNotificationsSuspended.Dispose();
        }
    }
}
