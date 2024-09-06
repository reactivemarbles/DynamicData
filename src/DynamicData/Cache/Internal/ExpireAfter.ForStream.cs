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
    public static class ForStream<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        public static IObservable<IChangeSet<TObject, TKey>> Create(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector,
            TimeSpan? pollingInterval = null,
            IScheduler? scheduler = null)
        {
            source.ThrowArgumentNullExceptionIfNull(nameof(source));
            timeSelector.ThrowArgumentNullExceptionIfNull(nameof(timeSelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(observer => (pollingInterval is { } pollingIntervalValue)
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
            private readonly ChangeAwareCache<TObject, TKey> _itemsCache;
            private readonly IObserver<IChangeSet<TObject, TKey>> _observer;
            private readonly List<ProposedExpiration> _proposedExpirationsQueue;
            private readonly IScheduler _scheduler;
            private readonly IDisposable _sourceSubscription;
            private readonly Func<TObject, TimeSpan?> _timeSelector;

            private bool _hasSourceCompleted;
            private ScheduledManagement? _nextScheduledManagement;

            protected SubscriptionBase(
                IObserver<IChangeSet<TObject, TKey>> observer,
                IScheduler? scheduler,
                IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector)
            {
                _observer = observer;
                _timeSelector = timeSelector;

                _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

                _expirationDueTimesByKey = new();
                _itemsCache = new();
                _proposedExpirationsQueue = new();

                _sourceSubscription = source
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

                        // The state of _expirationQueue is allowed to go out-of-sync with _itemStatesByKey,
                        // so make sure the item still needs to be removed, before removing it.
                        if (_expirationDueTimesByKey.TryGetValue(proposedExpiration.Key, out var expirationDueTime) && (expirationDueTime <= now))
                        {
                            _expirationDueTimesByKey.Remove(proposedExpiration.Key);

                            _itemsCache.Remove(proposedExpiration.Key);
                        }
                    }
                    _proposedExpirationsQueue.RemoveRange(0, proposedExpirationIndex);

                    // The scheduler only promises "best effort" to cancel scheduled operations, so we can end up with no items being expired.
                    var downstreamChanges = _itemsCache.CaptureChanges();
                    if (downstreamChanges.Count is not 0)
                        _observer.OnNext(downstreamChanges);

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

                // If we're out of items to expire, and the source has completed, we'll never have any further changes to publish.
                if ((_expirationDueTimesByKey.Count is 0) && _hasSourceCompleted)
                {
                    TryCancelNextScheduledManagement();

                    _observer.OnCompleted();

                    return;
                }

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
                                action: (_, @this) =>
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
                _hasSourceCompleted = true;

                // Postpone downstream completion if there are any expirations pending.
                if (_expirationDueTimesByKey.Count is 0)
                {
                    TryCancelNextScheduledManagement();

                    _observer.OnCompleted();
                }
            }

            private void OnSourceError(Exception error)
            {
                TryCancelNextScheduledManagement();

                _observer.OnError(error);
            }

            private void OnSourceNext(IChangeSet<TObject, TKey> upstreamChanges)
            {
                try
                {
                    var now = _scheduler.Now;

                    var haveExpirationsChanged = false;

                    foreach (var change in upstreamChanges.ToConcreteType())
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
                                    _itemsCache.AddOrUpdate(change.Current, change.Key);
                                }
                                break;

                            // Ignore Move changes completely, as this is functionally really just a fancy filter operator.

                            case ChangeReason.Remove:
                                ClearExpiration(change.Key);
                                _itemsCache.Remove(change.Key);
                                break;

                            case ChangeReason.Refresh:
                                _itemsCache.Refresh(change.Key);
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

                                    _itemsCache.AddOrUpdate(change.Current, change.Key);
                                }
                                break;
                        }
                    }

                    if (haveExpirationsChanged)
                        OnExpirationsChanged();

                    var downstreamChanges = _itemsCache.CaptureChanges();
                    if (downstreamChanges.Count is not 0)
                        _observer.OnNext(downstreamChanges);
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
                var oldDueTime = _expirationDueTimesByKey.TryGetValue(key, out var expirationDueTime)
                    ? expirationDueTime
                    : null as DateTimeOffset?;

                // Always update the item state, cause even if ExpireAt doesn't change, the item itself might have.
                _expirationDueTimesByKey[key] = dueTime;

                if (dueTime == oldDueTime)
                    return false;

                var insertionIndex = _proposedExpirationsQueue.BinarySearch(dueTime, static (expireAt, expiration) => expireAt.CompareTo(expiration.DueTime));
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
                    IObserver<IChangeSet<TObject, TKey>> observer,
                    IScheduler? scheduler,
                    IObservable<IChangeSet<TObject, TKey>> source,
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
                    IObserver<IChangeSet<TObject, TKey>> observer,
                    TimeSpan pollingInterval,
                    IScheduler? scheduler,
                    IObservable<IChangeSet<TObject, TKey>> source,
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
