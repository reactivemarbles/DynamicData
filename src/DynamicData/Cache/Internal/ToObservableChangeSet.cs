// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class ToObservableChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TimeSpan?>? _expireAfter;
    private readonly Func<TObject, TKey> _keySelector;
    private readonly int _limitSizeTo;
    private readonly IScheduler _scheduler;
    private readonly IObservable<IEnumerable<TObject>> _source;

    public ToObservableChangeSet(
        IObservable<TObject> source,
        Func<TObject, TKey> keySelector,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler)
    {
        _expireAfter = expireAfter;
        _keySelector = keySelector;
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
        Func<TObject, TKey> keySelector,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler)
    {
        _expireAfter = expireAfter;
        _keySelector = keySelector;
        _limitSizeTo = limitSizeTo;
        _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;
        _source = source;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run()
        => Observable.Create<IChangeSet<TObject, TKey>>(observer => new Subscription(
            source: _source,
            expireAfter: _expireAfter,
            keySelector: _keySelector,
            limitSizeTo: _limitSizeTo,
            observer: observer,
            scheduler: _scheduler));

    private sealed class Subscription
        : IDisposable
    {
        private readonly EvictionState? _evictionState;
        private readonly ExpirationState? _expirationState;
        private readonly Dictionary<TKey, ItemState> _itemStatesByKey;
        private readonly Func<TObject, TKey> _keySelector;
        private readonly IObserver<IChangeSet<TObject, TKey>> _observer;
        private readonly IScheduler _scheduler;
        private readonly IDisposable _sourceSubscription;

        private bool _hasSourceCompleted;
        private ScheduledExpiration? _scheduledExpiration;

        public Subscription(
            IObservable<IEnumerable<TObject>> source,
            Func<TObject, TimeSpan?>? expireAfter,
            Func<TObject, TKey> keySelector,
            int limitSizeTo,
            IObserver<IChangeSet<TObject, TKey>> observer,
            IScheduler scheduler)
        {
            _keySelector = keySelector;
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
                        ChangesBuffer = new(),
                        ExpireAfter = expireAfter,
                        Queue = new(capacity: limitSizeTo)
                    };

                _itemStatesByKey = new(capacity: limitSizeTo);
            }
            else
            {
                _expirationState = (expireAfter is null)
                    ? null
                    : new()
                    {
                        ChangesBuffer = new(),
                        ExpireAfter = expireAfter,
                        Queue = new()
                    };

                _itemStatesByKey = [];
            }

            _sourceSubscription = source
                .Synchronize(SynchronizationGate)
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

                            var changeSet = new ChangeSet<TObject, TKey>(capacity: (_evictionState is { } evictionState)
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

        // Instead of using a dedicated _synchronizationGate object, we can save an allocation by using any object that is never exposed to consumers.
        private object SynchronizationGate
            => _itemStatesByKey;

        public void Dispose()
        {
            lock (SynchronizationGate)
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
            ChangeSet<TObject, TKey> changeSet,
            ref bool hasExpirationQueueChanged)
        {
            var key = _keySelector.Invoke(item);
            var previousItemState = _itemStatesByKey.TryGetValue(key, out var existingItemState)
                ? existingItemState
                : null as ItemState?;

            // Perform processing for eviction behavior, if applicable
            if (_evictionState is { } evictionState)
            {
                // Backwards compatibility
                if (evictionState.LimitSizeTo is 0)
                {
                    return;
                }

                // Eviction is only applicable to adds, not replacements
                if (previousItemState is null)
                {
                    // If our size limit has been reached, evict the oldest item before adding a new one.
                    // Repeat removals until we drop below the limit, since items in the queue might have already expired.
                    while (_itemStatesByKey.Count >= evictionState.LimitSizeTo)
                    {
                        var keyToEvict = evictionState.Queue.Dequeue();

                        if (_itemStatesByKey.TryGetValue(keyToEvict, out var itemStateToEvict))
                        {
                            _itemStatesByKey.Remove(keyToEvict);
                            changeSet.Add(new(
                                reason: ChangeReason.Remove,
                                key: keyToEvict,
                                current: itemStateToEvict.Item));
                        }
                    }

                    evictionState.Queue.Enqueue(key);
                }
            }

            // Perform processing for expiration behavior, if applicable
            var expireAt = null as DateTimeOffset?;
            if (_expirationState is { } expirationState)
            {
                var previousExpireAt = previousItemState?.ExpireAt;
                var expireAfter = expirationState.ExpireAfter.Invoke(item);
                if (expireAfter is { } resolvedExpireAfter)
                {
                    // Truncate to milliseconds to promote batching expirations together.
                    var expireAtTicks = now.UtcTicks + resolvedExpireAfter.Ticks;
                    expireAt = new DateTimeOffset(ticks: expireAtTicks - (expireAtTicks % TimeSpan.TicksPerMillisecond), offset: TimeSpan.Zero);
                }

                // Queue the item for expiration if it's new and needs to expire, or if it's a replacement with a different expiration time.
                if ((expireAt is not null) && (expireAt != previousExpireAt))
                {
                    var insertionIndex = expirationState.Queue.BinarySearch(expireAt.Value, CompareExpireAtToExpiration);
                    if (insertionIndex < 0)
                    {
                        insertionIndex = ~insertionIndex;
                    }

                    expirationState.Queue.Insert(
                        index: insertionIndex,
                        item: new()
                        {
                            ExpireAt = expireAt.Value,
                            Key = key
                        });

                    hasExpirationQueueChanged = true;
                }
            }

            // Track the item's state, to be able to detect replacements later, and issue either an add or replace change for it.
            _itemStatesByKey[key] = new()
            {
                ExpireAt = expireAt,
                Item = item
            };
            changeSet.Add((previousItemState is null)
                ? new(
                    reason: ChangeReason.Add,
                    key: key,
                    current: item)
                : new(
                    reason: ChangeReason.Update,
                    key: key,
                    current: item,
                    previous: previousItemState.Value.Item));
        }

        private void HandleScheduledExpiration()
        {
            var expirationState = _expirationState!.Value;

            var now = _scheduler.Now;

            // Buffer removals, so we can optimize the allocation for the final changeset, or skip it entirely.
            // Also, so we can optimize removal from the queue as a range removal.
            var processedExpirationCount = 0;
            foreach (var expiration in expirationState.Queue)
            {
                if (expiration.ExpireAt > now)
                {
                    break;
                }

                ++processedExpirationCount;

                // If the item hasn't already been evicted, or had its expiration time change, formally remove it
                if (_itemStatesByKey.TryGetValue(expiration.Key, out var itemState) && (itemState.ExpireAt <= now))
                {
                    _itemStatesByKey.Remove(expiration.Key);
                    expirationState.ChangesBuffer.Add(new(
                        reason: ChangeReason.Remove,
                        key: expiration.Key,
                        current: itemState.Item));
                }
            }

            expirationState.Queue.RemoveRange(0, processedExpirationCount);

            // We can end up with no changes here for a couple of reasons:
            // * An item's expiration time can change
            // * When items are evicted due to the size limit, it still remains in the expiration queue.
            // * The scheduler only promises "best effort" to cancel scheduled operations.
            if (expirationState.ChangesBuffer.Count is not 0)
            {
                _observer.OnNext(new ChangeSet<TObject, TKey>(expirationState.ChangesBuffer));

                expirationState.ChangesBuffer.Clear();
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
                            lock (@this.SynchronizationGate)
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

        private readonly struct ItemState
        {
            public required DateTimeOffset? ExpireAt { get; init; }

            public required TObject Item { get; init; }
        }

        private readonly struct EvictionState
        {
            public required int LimitSizeTo { get; init; }

            public required Queue<TKey> Queue { get; init; }
        }

        private readonly struct Expiration
        {
            public required DateTimeOffset ExpireAt { get; init; }

            public required TKey Key { get; init; }
        }

        private readonly struct ExpirationState
        {
            public required List<Change<TObject, TKey>> ChangesBuffer { get; init; }

            public required Func<TObject, TimeSpan?> ExpireAfter { get; init; }

            // Potential performance improvement: Instead of List<T>, use PriorityQueue<T> available in .NET 6+, or an equivalent.
            public required List<Expiration> Queue { get; init; }
        }

        private readonly struct ScheduledExpiration
        {
            public required IDisposable Cancellation { get; init; }

            public required DateTimeOffset DueTime { get; init; }
        }
    }
}
