// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal static partial class ExpireAfter
{
    public static class ForSource<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> Create(
            ISourceCache<TObject, TKey> source,
            Func<TObject, TimeSpan?> timeSelector,
            TimeSpan? pollingInterval = null,
            IScheduler? scheduler = null)
        {
            source.ThrowArgumentNullExceptionIfNull(nameof(source));
            timeSelector.ThrowArgumentNullExceptionIfNull(nameof(timeSelector));

            return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(observer => (pollingInterval is { } pollingIntervalValue)
                ? new PollingSubscription(
                    observer: observer,
                    pollingInterval: pollingIntervalValue,
                    scheduler: scheduler,
                    source: source,
                    timeSelector: timeSelector)
                : new OnDemandSubscription(
                    observer: observer,
                    scheduler: scheduler,
                    source: source,
                    timeSelector: timeSelector));
        }

        private abstract class SubscriptionBase
            : IDisposable
        {
            private readonly Dictionary<TKey, DateTimeOffset> _expirationDueTimesByKey;
            private readonly IObserver<IEnumerable<KeyValuePair<TKey, TObject>>> _observer;
            private readonly Action<ISourceUpdater<TObject, TKey>> _onEditingSource;
            private readonly List<ProposedExpiration> _proposedExpirationsQueue;
            private readonly List<KeyValuePair<TKey, TObject>> _removedItemsBuffer;
            private readonly IScheduler _scheduler;
            private readonly ISourceCache<TObject, TKey> _source;
            private readonly IDisposable _sourceSubscription;
            private readonly Func<TObject, TimeSpan?> _timeSelector;

            private bool _hasSourceCompleted;
            private ScheduledManagement? _nextScheduledManagement;

            protected SubscriptionBase(
                IObserver<IEnumerable<KeyValuePair<TKey, TObject>>> observer,
                IScheduler? scheduler,
                ISourceCache<TObject, TKey> source,
                Func<TObject, TimeSpan?> timeSelector)
            {
                _observer = observer;
                _source = source;
                _timeSelector = timeSelector;

                _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

                _onEditingSource = OnEditingSource;

                _expirationDueTimesByKey = new();
                _proposedExpirationsQueue = new();
                _removedItemsBuffer = new();

                _sourceSubscription = source
                    .Connect()
                    // It's important to set this flag outside the context of a lock, because it'll be read outside of lock as well.
                    .Finally(() => _hasSourceCompleted = true)
                    .Synchronize(SynchronizationGate)
                    .SubscribeSafe(
                        onNext: OnSourceNext,
                        onError: OnSourceError,
                        onCompleted: OnSourceCompleted);
            }

            public void Dispose()
            {
                lock (SynchronizationGate)
                {
                    _sourceSubscription.Dispose();

                    TryCancelNextScheduledManagement();
                }
            }

            protected IScheduler Scheduler
                => _scheduler;

            // Instead of using a dedicated _synchronizationGate object, we can save an allocation by using any object that is never exposed to public consumers.
            protected object SynchronizationGate
                => _expirationDueTimesByKey;

            protected abstract DateTimeOffset? GetNextManagementDueTime();

            protected DateTimeOffset? GetNextProposedExpirationDueTime()
                => _proposedExpirationsQueue.Count is 0
                    ? null
                    : _proposedExpirationsQueue[0].DueTime;

            protected abstract void OnExpirationsManaged(DateTimeOffset dueTime);

            private void ClearExpiration(TKey key)
                // This is what puts the "proposed" in _proposedExpirationsQueue.
                // Finding the position of the item to remove from the queue would be O(log n), at best,
                // so just leave it and flush it later during normal processing of the queue.
                => _expirationDueTimesByKey.Remove(key);

            private void ManageExpirations()
            {
                // This check is needed, to make sure we don't try and call .Edit() on a disposed _source,
                // since the scheduler only promises "best effort" to cancel a scheduled action.
                // It's safe to skip locking here becuase once this flag is set, it's never unset.
                if (_hasSourceCompleted)
                    return;

                // Putting the entire management process here inside a .Edit() call for a couple of reasons:
                //  - It keeps the edit delegate from becoming a closure, so we can use a reusable cached delegate.
                //  - It batches multiple expirations occurring at the same time into one source operation, so it only emits one changeset.
                //  - It eliminates the possibility of _itemStatesByKey and other internal state becoming out-of-sync with _source, by effectively locking _source.
                //  - It eliminates a rare deadlock that I honestly can't fully explain, but was able to reproduce reliably with few hundred iterations of the ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe test.
                _source.Edit(_onEditingSource);
            }

            private void OnEditingSource(ISourceUpdater<TObject, TKey> updater)
            {
                lock (SynchronizationGate)
                {
                    // The scheduler only promises "best effort" to cancel scheduled operations, so we need to make sure.
                    if (_nextScheduledManagement is not { } thisScheduledManagement)
                        return;

                    _nextScheduledManagement = null;

                    var now = Scheduler.Now;

                    // Buffer removals, so we can optimize the allocation for the final changeset, or skip it entirely.
                    // Also, so we can optimize removal from the queue as a range removal.
                    var proposedExpirationIndex = 0;
                    for (; proposedExpirationIndex < _proposedExpirationsQueue.Count; ++proposedExpirationIndex)
                    {
                        var proposedExpiration = _proposedExpirationsQueue[proposedExpirationIndex];
                        if (proposedExpiration.DueTime > now)
                        {
                            break;
                        }

                        // The state of _expirationQueue is allowed to go out-of-sync with _expirationDueTimesByKey,
                        // so make sure the item still needs to be removed, before removing it.
                        if (_expirationDueTimesByKey.TryGetValue(proposedExpiration.Key, out var expirationDueTime) && (expirationDueTime <= now))
                        {
                            _expirationDueTimesByKey.Remove(proposedExpiration.Key);

                            _removedItemsBuffer.Add(new(
                                key: proposedExpiration.Key,
                                value: updater.Lookup(proposedExpiration.Key).Value));

                            updater.RemoveKey(proposedExpiration.Key);
                        }
                    }
                    _proposedExpirationsQueue.RemoveRange(0, proposedExpirationIndex);

                    // We can end up with no expiring items here because the scheduler only promises "best effort" to cancel scheduled operations,
                    // or because of a race condition with the source.
                    if (_removedItemsBuffer.Count is not 0)
                    {
                        _observer.OnNext(_removedItemsBuffer.ToArray());

                        _removedItemsBuffer.Clear();
                    }

                    OnExpirationsManaged(thisScheduledManagement.DueTime);

                    // We just changed the expirations queue, so run cleanup and management scheduling.
                    OnExpirationsChanged();
                }
            }

            private void OnExpirationsChanged()
            {
                // Clear out any expirations at the front of the queue that are no longer valid.
                var removeToIndex = _proposedExpirationsQueue.FindIndex(expiration => _expirationDueTimesByKey.ContainsKey(expiration.Key));
                if (removeToIndex > 0)
                    _proposedExpirationsQueue.RemoveRange(0, removeToIndex);

                // Check if we need to re-schedule the next management operation
                if (GetNextManagementDueTime() is { } nextManagementDueTime)
                {
                    if (_nextScheduledManagement?.DueTime != nextManagementDueTime)
                    {
                        if (_nextScheduledManagement is { } nextScheduledManagement)
                            nextScheduledManagement.Cancellation.Dispose();

                        _nextScheduledManagement = new()
                        {
                            Cancellation = _scheduler.Schedule(
                                state: this,
                                dueTime: nextManagementDueTime,
                                action: static (_, @this) =>
                                {
                                    @this.ManageExpirations();

                                    return Disposable.Empty;
                                }),
                            DueTime = nextManagementDueTime
                        };
                    }
                }
                else
                {
                    TryCancelNextScheduledManagement();
                }
            }

            private void OnSourceCompleted()
            {
                // If the source completes, we can no longer remove items from it, so any pending expirations are moot.
                TryCancelNextScheduledManagement();

                _observer.OnCompleted();
            }

            private void OnSourceError(Exception error)
            {
                TryCancelNextScheduledManagement();

                _observer.OnError(error);
            }

            private void OnSourceNext(IChangeSet<TObject, TKey> changes)
            {
                try
                {
                    var now = _scheduler.Now;

                    var haveExpirationsChanged = false;

                    foreach (var change in changes.ToConcreteType())
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                {
                                    if (_timeSelector.Invoke(change.Current) is { } expireAfter)
                                    {
                                        haveExpirationsChanged |= TrySetExpiration(
                                            key: change.Key,
                                            dueTime: now + expireAfter);
                                    }
                                }
                                break;

                            case ChangeReason.Remove:
                                ClearExpiration(change.Key);
                                haveExpirationsChanged = true;
                                break;

                            case ChangeReason.Update:
                                {
                                    if (_timeSelector.Invoke(change.Current) is { } expireAfter)
                                    {
                                        haveExpirationsChanged = TrySetExpiration(
                                            key: change.Key,
                                            dueTime: now + expireAfter);
                                    }
                                    else
                                    {
                                        ClearExpiration(change.Key);
                                        haveExpirationsChanged = true;
                                    }
                                }
                                break;
                        }
                    }

                    if (haveExpirationsChanged)
                        OnExpirationsChanged();
                }
                catch (Exception error)
                {
                    TryCancelNextScheduledManagement();

                    _observer.OnError(error);
                }
            }

            private void TryCancelNextScheduledManagement()
            {
                _nextScheduledManagement?.Cancellation.Dispose();
                _nextScheduledManagement = null;
            }

            private bool TrySetExpiration(
                TKey key,
                DateTimeOffset dueTime)
            {
                var oldDueTime = _expirationDueTimesByKey.TryGetValue(key, out var existingDueTime)
                    ? existingDueTime
                    : null as DateTimeOffset?;

                // Always update the item state, cause even if ExpireAt doesn't change, the item itself might have.
                _expirationDueTimesByKey[key] = dueTime;

                if (dueTime == oldDueTime)
                    return false;

                var insertionIndex = _proposedExpirationsQueue.BinarySearch(dueTime, static (dueTime, expiration) => dueTime.CompareTo(expiration.DueTime));
                if (insertionIndex < 0)
                    insertionIndex = ~insertionIndex;

                _proposedExpirationsQueue.Insert(
                    index: insertionIndex,
                    item: new()
                    {
                        DueTime = dueTime,
                        Key = key
                    });

                // Intentionally not removing the old expiration for this item, if applicable, see ClearExpiration()

                return true;
            }

            private readonly struct ProposedExpiration
            {
                public required DateTimeOffset DueTime { get; init; }

                public required TKey Key { get; init; }
            }

            private readonly struct ScheduledManagement
            {
                public required IDisposable Cancellation { get; init; }

                public required DateTimeOffset DueTime { get; init; }
            }
        }

        private sealed class OnDemandSubscription
            : SubscriptionBase
        {
            public OnDemandSubscription(
                    IObserver<IEnumerable<KeyValuePair<TKey, TObject>>> observer,
                    IScheduler? scheduler,
                    ISourceCache<TObject, TKey> source,
                    Func<TObject, TimeSpan?> timeSelector)
                : base(
                    observer,
                    scheduler,
                    source,
                    timeSelector)
            {
            }

            protected override DateTimeOffset? GetNextManagementDueTime()
                => GetNextProposedExpirationDueTime();

            protected override void OnExpirationsManaged(DateTimeOffset dueTime)
            {
            }
        }

        private sealed class PollingSubscription
            : SubscriptionBase
        {
            private readonly TimeSpan _pollingInterval;

            private DateTimeOffset _lastManagementDueTime;

            public PollingSubscription(
                    IObserver<IEnumerable<KeyValuePair<TKey, TObject>>> observer,
                    TimeSpan pollingInterval,
                    IScheduler? scheduler,
                    ISourceCache<TObject, TKey> source,
                    Func<TObject, TimeSpan?> timeSelector)
                : base(
                    observer,
                    scheduler,
                    source,
                    timeSelector)
            {
                _pollingInterval = pollingInterval;

                _lastManagementDueTime = Scheduler.Now;
            }

            protected override DateTimeOffset? GetNextManagementDueTime()
            {
                var now = Scheduler.Now;
                var nextDueTime = _lastManagementDueTime + _pollingInterval;

                // Throttle down the polling frequency if polls are taking longer than the ideal interval.
                return (nextDueTime > now)
                    ? nextDueTime
                    : now;
            }

            protected override void OnExpirationsManaged(DateTimeOffset dueTime)
                => _lastManagementDueTime = dueTime;
        }
    }
}
