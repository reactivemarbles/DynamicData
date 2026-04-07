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

    private readonly DeliveryQueue<NotificationItem> _notifications;

    private int _editLevel; // The level of recursion in editing.

    public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        _readerWriter = new ReaderWriter<TObject, TKey>();
        _notifications = new DeliveryQueue<NotificationItem>(_locker, DeliverNotification);
        _suspensionTracker = new(() => new SuspensionTracker());

        var loader = source.Subscribe(
            changeSet =>
            {
                using var notifications = _notifications.AcquireLock();

                var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                var changes = _readerWriter.Write(changeSet, previewHandler, _changes.HasObservers);

                if (changes is not null)
                {
                    var isSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.AreNotificationsSuspended;
                    var isCountSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.IsCountSuspended;
                    notifications.Enqueue(
                        NotificationItem.CreateChanges(changes, _readerWriter.Count, isSuspended, isCountSuspended),
                        countAsPending: !isSuspended);
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
        _notifications = new DeliveryQueue<NotificationItem>(_locker, DeliverNotification);
        _suspensionTracker = new(() => new SuspensionTracker());

        _cleanUp = Disposable.Create(NotifyCompleted);
    }

    public int Count => _readerWriter.Count;

    public IObservable<int> CountChanged =>
        Observable.Create<int>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                var source = _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();
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
            var isSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.AreNotificationsSuspended;
            var isCountSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.IsCountSuspended;
            notifications.Enqueue(
                NotificationItem.CreateChanges(changes, _readerWriter.Count, isSuspended, isCountSuspended),
                countAsPending: !isSuspended);
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
            var isSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.AreNotificationsSuspended;
            var isCountSuspended = _suspensionTracker.IsValueCreated && _suspensionTracker.Value.IsCountSuspended;
            notifications.Enqueue(
                NotificationItem.CreateChanges(changes, _readerWriter.Count, isSuspended, isCountSuspended),
                countAsPending: !isSuspended);
        }
    }

    private IObservable<IChangeSet<TObject, TKey>> CreateConnectObservable(Func<TObject, bool>? predicate, bool suppressEmptyChangeSets) =>
        Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                // Skip pending notifications to avoid duplicating items already in the snapshot.
                var skipCount = readLock.PendingCount;

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
            });

    private IObservable<Change<TObject, TKey>> CreateWatchObservable(TKey key) =>
        Observable.Create<Change<TObject, TKey>>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                var skipCount = readLock.PendingCount;

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
        notifications.Enqueue(NotificationItem.CreateCompleted());
    }

    private void NotifyError(Exception ex)
    {
        using var notifications = _notifications.AcquireLock();
        notifications.Enqueue(NotificationItem.CreateError(ex));
    }

    private bool DeliverNotification(NotificationItem item)
    {
        switch (item.Kind)
        {
            case NotificationKind.Completed:
                _changes.OnCompleted();
                _changesPreview.OnCompleted();

                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnCompleted();
                }

                if (_suspensionTracker.IsValueCreated)
                {
                    lock (_locker)
                    {
                        _suspensionTracker.Value.Dispose();
                    }
                }
                return false;

            case NotificationKind.Error:
                _changesPreview.OnError(item.Error!);
                _changes.OnError(item.Error!);

                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnError(item.Error!);
                }

                if (_suspensionTracker.IsValueCreated)
                {
                    lock (_locker)
                    {
                        _suspensionTracker.Value.Dispose();
                    }
                }
                return false;

            case NotificationKind.CountOnly:
                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnNext(item.Count);
                }

                return true;

            default:
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

                return true;
        }
    }

    private void ResumeCount()
    {
        using var notifications = _notifications.AcquireLock();
        Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Count without Suspend Count instance");

        if (_suspensionTracker.Value.ResumeCount() && _countChanged.IsValueCreated)
        {
            notifications.Enqueue(NotificationItem.CreateCountOnly(_readerWriter.Count));
        }
    }

    private void ResumeNotifications()
    {
        using var notifications = _notifications.AcquireLock();
        Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Notifications without Suspend Notifications instance");

        var (resumedChanges, emitResume) = _suspensionTracker.Value.ResumeNotifications();
        if (resumedChanges is not null)
        {
            notifications.Enqueue(
                NotificationItem.CreateChanges(resumedChanges, _readerWriter.Count, isSuspended: false, isCountSuspended: false),
                countAsPending: true);
        }

        if (emitResume)
        {
            _suspensionTracker.Value.EmitResumeNotification();
        }
    }

    private enum NotificationKind
    {
        Changes,
        CountOnly,
        Completed,
        Error,
    }

    private readonly record struct NotificationItem
    {
        public NotificationKind Kind { get; }

        public ChangeSet<TObject, TKey> Changes { get; }

        public int Count { get; }

        public bool IsSuspended { get; }

        public bool IsCountSuspended { get; }

        public Exception? Error { get; }

        private NotificationItem(NotificationKind kind, ChangeSet<TObject, TKey> changes, int count = 0, bool isSuspended = false, bool isCountSuspended = false, Exception? error = null)
        {
            Kind = kind;
            Changes = changes;
            Count = count;
            IsSuspended = isSuspended;
            IsCountSuspended = isCountSuspended;
            Error = error;
        }

        public static NotificationItem CreateChanges(ChangeSet<TObject, TKey> changes, int count, bool isSuspended, bool isCountSuspended) =>
            new(NotificationKind.Changes, changes, count, isSuspended, isCountSuspended);

        public static NotificationItem CreateCountOnly(int count) =>
            new(NotificationKind.CountOnly, [], count: count);

        public static NotificationItem CreateCompleted() =>
            new(NotificationKind.Completed, []);

        public static NotificationItem CreateError(Exception error) =>
            new(NotificationKind.Error, [], error: error);
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

                return (changes, _notifySuspendCount == 0);
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
