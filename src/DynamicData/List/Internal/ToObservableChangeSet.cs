// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class ToObservableChangeSet<TObject>
    where TObject : notnull
{
    private readonly Func<TObject, TimeSpan?>? _expireAfter;
    private readonly int _limitSizeTo;
    private readonly IScheduler _scheduler;
    private readonly IObservable<IEnumerable<TObject>> _source;

    public ToObservableChangeSet(
        IObservable<TObject> source,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler)
    {
        _expireAfter = expireAfter;
        _limitSizeTo = limitSizeTo;
        _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

        _source = Observable.Create<IEnumerable<TObject>>(observer =>
        {
            // Reusable buffer, to avoid allocating per-item
            var buffer = new TObject[1];

            return source.SubscribeSafe(Observer.Create<TObject>(
                onNext: item =>
                {
                    buffer[0] = item;
                    observer.OnNext(buffer);
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted));
        });
    }

    public ToObservableChangeSet(
        IObservable<IEnumerable<TObject>> source,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler)
    {
        _expireAfter = expireAfter;
        _limitSizeTo = limitSizeTo;
        _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;
        _source = source;
    }

    public IObservable<IChangeSet<TObject>> Run()
        => Observable.Create<IChangeSet<TObject>>(observer => new Subscription(
            source: _source,
            expireAfter: _expireAfter,
            limitSizeTo: _limitSizeTo,
            observer: observer,
            scheduler: _scheduler));

    private sealed class Subscription
        : IDisposable
    {
        private readonly EvictionState? _evictionState;
        private readonly ExpirationState? _expirationState;
        private readonly IObserver<IChangeSet<TObject>> _observer;
        private readonly IScheduler _scheduler;
        private readonly IDisposable _sourceSubscription;
        private readonly object _synchronizationGate;

        private int _currentItemCount;
        private bool _hasSourceCompleted;
        private ScheduledExpiration? _scheduledExpiration;

        public Subscription(
            IObservable<IEnumerable<TObject>> source,
            Func<TObject, TimeSpan?>? expireAfter,
            int limitSizeTo,
            IObserver<IChangeSet<TObject>> observer,
            IScheduler scheduler)
        {
            _observer = observer;
            _scheduler = scheduler;

            if (limitSizeTo >= 0)
            {
                _evictionState = new()
                {
                    LimitSizeTo = limitSizeTo,
                    Queue = new(capacity: limitSizeTo)
                };

                _expirationState = (expireAfter is null)
                    ? null
                    : new()
                    {
                        RemovalsBuffer = new(),
                        ExpireAfter = expireAfter,
                        Queue = new(capacity: limitSizeTo)
                    };
            }
            else
            {
                _expirationState = (expireAfter is null)
                    ? null
                    : new()
                    {
                        RemovalsBuffer = new(),
                        ExpireAfter = expireAfter,
                        Queue = new()
                    };
            }

            _synchronizationGate = new();

            _sourceSubscription = source
                .Synchronize(_synchronizationGate)
                .SubscribeSafe(Observer.Create<IEnumerable<TObject>>(
                    onNext: items =>
                    {
                        try
                        {
                            var now = _scheduler.Now;

                            var hasExpirationQueueChanged = false;

                            var itemCount = items switch
                            {
                                ICollection<TObject> itemsCollection => itemsCollection.Count,
                                IReadOnlyCollection<TObject> itemsCollection => itemsCollection.Count,
                                _ => 0
                            };

                            var changeSet = new ChangeSet<TObject>(capacity: (_evictionState is { } evictionState)
                                ? Math.Max(itemCount + evictionState.Queue.Count - evictionState.LimitSizeTo, 0)
                                : itemCount);

                            if (items is IReadOnlyList<TObject> itemsList)
                            {
                                for (var i = 0; i < itemsList.Count; ++i)
                                {
                                    HandleIncomingItem(itemsList[i], now, changeSet, ref hasExpirationQueueChanged);
                                }
                            }
                            else
                            {
                                foreach (var item in items)
                                {
                                    HandleIncomingItem(item, now, changeSet, ref hasExpirationQueueChanged);
                                }
                            }

                            if (hasExpirationQueueChanged)
                            {
                                OnExpirationQueueChanged();
                            }

                            observer.OnNext(changeSet);
                        }
                        catch (Exception error)
                        {
                            TearDownStates();

                            observer.OnError(error);
                        }
                    },
                    onError: error =>
                    {
                        TearDownStates();

                        observer.OnError(error);
                    },
                    onCompleted: () =>
                    {
                        _hasSourceCompleted = true;

                        // If there are pending expirations scheduled, wait to complete the stream until they're done
                        if (_expirationState is null or { Queue.Count: 0 })
                        {
                            observer.OnCompleted();
                        }
                    }));
        }

        public void Dispose()
        {
            lock (_synchronizationGate)
            {
                _sourceSubscription.Dispose();

                TearDownStates();
            }
        }

        private static int CompareExpireAtToExpiration(DateTimeOffset expireAt, Expiration expiration)
            => expireAt.CompareTo(expiration.ExpireAt);

        private void HandleIncomingItem(
            TObject item,
            DateTimeOffset now,
            ChangeSet<TObject> changeSet,
            ref bool hasExpirationQueueChanged)
        {
            // Perform processing for eviction behavior, if applicable
            if (_evictionState is { } evictionState)
            {
                // Backwards compatibility
                if (evictionState.LimitSizeTo is 0)
                {
                    return;
                }

                // If our size limit has been reached, evict the oldest item before adding a new one.
                // Repeat removals until we drop below the limit, since items in the queue might have already expired.
                if (evictionState.Queue.Count >= evictionState.LimitSizeTo)
                {
                    var itemToEvict = evictionState.Queue[0];
                    evictionState.Queue.RemoveAt(0);

                    // Need to synchronize the expiration queue, if applicable, to keep the indexes stored there correct.
                    if (_expirationState is { Queue: var expirationQueue })
                    {
                        for (var i = 0; i < expirationQueue.Count;)
                        {
                            if (expirationQueue[i].Index == 0)
                            {
                                expirationQueue.RemoveAt(i);
                                continue;
                            }

                            var expiration = expirationQueue[i];
                            --expiration.Index;
                            expirationQueue[i] = expiration;

                            ++i;
                        }
                    }

                    changeSet.Add(new(
                        reason: ListChangeReason.Remove,
                        current: itemToEvict,
                        index: 0));
                    --_currentItemCount;
                }

                evictionState.Queue.Add(item);
            }

            // Perform processing for expiration behavior, if applicable
            if (_expirationState is { } expirationState)
            {
                var expireAfter = expirationState.ExpireAfter.Invoke(item);
                if (expireAfter is { } resolvedExpireAfter)
                {
                    // Truncate to milliseconds to promote batching expirations together.
                    var expireAtTicks = now.UtcTicks + resolvedExpireAfter.Ticks;
                    var expireAt = new DateTimeOffset(ticks: expireAtTicks - (expireAtTicks % TimeSpan.TicksPerMillisecond), offset: TimeSpan.Zero);

                    var insertionIndex = expirationState.Queue.BinarySearch(expireAt, CompareExpireAtToExpiration);
                    if (insertionIndex < 0)
                    {
                        insertionIndex = ~insertionIndex;
                    }

                    expirationState.Queue.Insert(
                        index: insertionIndex,
                        item: new()
                        {
                            ExpireAt = expireAt,
                            Index = _currentItemCount,
                            Item = item
                        });

                    hasExpirationQueueChanged = true;
                }
            }

            changeSet.Add(new(
                reason: ListChangeReason.Add,
                current: item,
                index: _currentItemCount));
            ++_currentItemCount;
        }

        private void HandleScheduledExpiration()
        {
            var expirationState = _expirationState!.Value;

            var now = _scheduler.Now;

            // Buffer removals, so we can sort them and generate adjusted indexes, in the event of many items being removed at once.
            // Also, so we can optimize away the changeSet allocation and publication, if possible.
            // Also, so we can optimize removal from the queue as a range removal.
            foreach (var expiration in expirationState.Queue)
            {
                if (expiration.ExpireAt > now)
                {
                    break;
                }

                expirationState.RemovalsBuffer.Add(new(
                    key: expiration.Index,
                    value: expiration.Item));
            }

            // It's theoretically possible to end up with no changes here,
            // as the scheduler only promises "best effort" to cancel scheduled operations.
            if (expirationState.RemovalsBuffer.Count is not 0)
            {
                expirationState.Queue.RemoveRange(0, expirationState.RemovalsBuffer.Count);

                expirationState.RemovalsBuffer.Sort(static (x, y) => x.Key.CompareTo(y.Key));

                var evictionQueue = _evictionState?.Queue;

                var changeSet = new ChangeSet<TObject>(capacity: expirationState.RemovalsBuffer.Count);
                for (var i = 0; i < expirationState.RemovalsBuffer.Count; ++i)
                {
                    var removal = expirationState.RemovalsBuffer[i];
                    var indexToRemove = removal.Key - i;

                    changeSet.Add(new(
                        reason: ListChangeReason.Remove,
                        current: removal.Value,
                        index: indexToRemove));
                    --_currentItemCount;

                    // Adjust indexes for all remaining items in the queue.
                    for (var j = 0; j < expirationState.Queue.Count; ++j)
                    {
                        var expiration = expirationState.Queue[j];
                        if (expiration.Index > indexToRemove)
                        {
                            --expiration.Index;
                        }

                        expirationState.Queue[j] = expiration;
                    }

                    // Clear expiring items out of the eviction queue as well, if applicable.
                    evictionQueue?.RemoveAt(indexToRemove);
                }

                expirationState.RemovalsBuffer.Clear();

                _observer.OnNext(changeSet);
            }

            OnExpirationQueueChanged();
        }

        private void OnExpirationQueueChanged()
        {
            var expirationState = _expirationState!.Value;

            // If there aren't any items queued to expire, check to see if the stream should be terminated (I.E. we just expired the last item).
            // Otherwise, make sure we have an operation scheduled to handle the next expiration.
            if (expirationState.Queue.Count is 0)
            {
                if (_hasSourceCompleted)
                {
                    _observer.OnCompleted();
                }
            }
            else
            {
                // If there's already a scheduled operation, and it doesn't match the current next-item-to-expire time, wipe it out and re-schedule it.
                var nextExpireAt = expirationState.Queue[0].ExpireAt;
                if (_scheduledExpiration is { } scheduledExpiration)
                {
                    if (scheduledExpiration.DueTime != nextExpireAt)
                    {
                        scheduledExpiration.Cancellation.Dispose();
                        _scheduledExpiration = null;
                    }
                    else
                    {
                        return;
                    }
                }

                _scheduledExpiration = new()
                {
                    Cancellation = _scheduler.Schedule(
                        state: this,
                        dueTime: nextExpireAt,
                        action: static (_, @this) =>
                        {
                            lock (@this._synchronizationGate)
                            {
                                @this._scheduledExpiration = null;

                                @this.HandleScheduledExpiration();
                            }

                            return Disposable.Empty;
                        }),
                    DueTime = nextExpireAt
                };
            }
        }

        private void TearDownStates()
        {
            _scheduledExpiration?.Cancellation.Dispose();
            _scheduledExpiration = null;

            _evictionState?.Queue.Clear();

            _expirationState?.Queue.Clear();
        }

        private readonly struct EvictionState
        {
            public required int LimitSizeTo { get; init; }

            public required List<TObject> Queue { get; init; }
        }

        private struct Expiration
        {
            public required DateTimeOffset ExpireAt { get; init; }

            public required int Index { get; set; }

            public required TObject Item { get; init; }
        }

        private readonly struct ExpirationState
        {
            public required List<KeyValuePair<int, TObject>> RemovalsBuffer { get; init; }

            public required Func<TObject, TimeSpan?> ExpireAfter { get; init; }

            public required List<Expiration> Queue { get; init; }
        }

        private readonly struct ScheduledExpiration
        {
            public required IDisposable Cancellation { get; init; }

            public required DateTimeOffset DueTime { get; init; }
        }
    }
}
