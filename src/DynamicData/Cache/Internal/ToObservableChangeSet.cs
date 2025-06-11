// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal static class ToObservableChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public static IObservable<IChangeSet<TObject, TKey>> Create(
            IObservable<TObject> source,
            Func<TObject, TKey> keySelector,
            Func<TObject, TimeSpan?>? expireAfter,
            int limitSizeTo,
            IScheduler? scheduler)
        => Observable.Create<IChangeSet<TObject, TKey>>(downstreamObserver =>
        {
            var buffer = new TObject[1];

            return Create(
                    source: source
                        .Select(item =>
                        {
                            buffer[0] = item;
                            return buffer;
                        }),
                    keySelector: keySelector,
                    expireAfter: expireAfter,
                    limitSizeTo: limitSizeTo,
                    scheduler: scheduler)
                .SubscribeSafe(downstreamObserver);
        });

    public static IObservable<IChangeSet<TObject, TKey>> Create(
            IObservable<IEnumerable<TObject>> source,
            Func<TObject, TKey> keySelector,
            Func<TObject, TimeSpan?>? expireAfter,
            int limitSizeTo,
            IScheduler? scheduler)
        => Observable.Create<IChangeSet<TObject, TKey>>(downstreamObserver => new Subscription(
            downstreamObserver: downstreamObserver,
            expireAfter: expireAfter,
            keySelector: keySelector,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler,
            source: source));

    private sealed class Subscription
        : IDisposable
    {
        private readonly ChangeAwareCache<TObject, TKey> _downstreamItems;
        private readonly IObserver<IChangeSet<TObject, TKey>> _downstreamObserver;
        private readonly Queue<TKey> _evictionQueue;
        private readonly List<Expiration> _expirationQueue;
        private readonly Func<TObject, TimeSpan?>? _expireAfter;
        private readonly Dictionary<TKey, DateTimeOffset> _expireAtsByKey;
        private readonly Func<TObject, TKey> _keySelector;
        private readonly int _limitSizeTo;
        private readonly IScheduler _scheduler;
        private readonly IDisposable _sourceSubscription;
        #if NET9_0_OR_GREATER
        private readonly Lock _synchronizationGate;
        #else
        private readonly object _synchronizationGate;
        #endif

        private bool _hasInitialized;
        private bool _hasSourceCompleted;
        private ScheduledExpiration? _scheduledExpiration;

        public Subscription(
            IObserver<IChangeSet<TObject, TKey>> downstreamObserver,
            Func<TObject, TimeSpan?>? expireAfter,
            Func<TObject, TKey> keySelector,
            int limitSizeTo,
            IScheduler? scheduler,
            IObservable<IEnumerable<TObject>> source)
        {
            _downstreamItems = new();
            _downstreamObserver = downstreamObserver;
            _evictionQueue = new();
            _expirationQueue = new();
            _expireAfter = expireAfter;
            _expireAtsByKey = new();
            _keySelector = keySelector;
            _limitSizeTo = limitSizeTo;
            _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;
            _synchronizationGate = new();

            _sourceSubscription = source.SubscribeSafe(
                onNext: OnSourceNext,
                onError: downstreamObserver.OnError,
                onCompleted: OnSourceCompleted);

            // Make sure we always publish an initial changeset, if subscribing to the source didn't generate one.
            // Also make sure we never complete, before the initial changeset.
            lock (_synchronizationGate)
            {
                TryPublishDownstreamChanges();
                TryPublishCompletion();
            }
        }

        public void Dispose()
        {
            _sourceSubscription.Dispose();
            _scheduledExpiration?.Cancellation.Dispose();
        }

        private IDisposable OnScheduledExpirationInvoked(
            IScheduler scheduler,
            Expiration intendedExpiration)
        {
            try
            {
                ScheduledExpiration? unfinishedExpiration;

                lock (_synchronizationGate)
                {
                    // There is no longer an expiration scheduled, we're in it
                    _scheduledExpiration = null;

                    // Scan the queue of expirations to identify all of them that are due.
                    var processedExpirationCount = 0;
                    foreach (var expiration in _expirationQueue)
                    {
                        // Inaccuracies in real-world scheduler timers make it possible for an item to be invoked slightly before its due time,
                        // so we're going to expire based on the timestamp of the "intended" expiration that this was scheduled for.
                        if (expiration.ExpireAt > intendedExpiration.ExpireAt)
                            break;

                        ++processedExpirationCount;

                        // The queue is not guaranteed to be up-to-date compared to _downstreamItems or _expireAtsByKey,
                        // so before we remove an item, make sure it still needs to be removed.
                        if (_expireAtsByKey.TryGetValue(expiration.Key, out var expireAt)
                            && (expireAt <= intendedExpiration.ExpireAt))
                        {
                            _downstreamItems.Remove(expiration.Key);
                            _expireAtsByKey.Remove(expiration.Key);
                        }
                    }

                    // Since the queue is a List<T> we can slightly-optimize by removing all the processed ones in one go.
                    _expirationQueue.RemoveRange(0, processedExpirationCount);

                    unfinishedExpiration = TryBeginSchedulingExpiration();

                    TryPublishDownstreamChanges();
                    TryPublishCompletion();
                }

                if (unfinishedExpiration is ScheduledExpiration unfinishedExpirationValue)
                    FinishSchedulingExpiration(unfinishedExpirationValue, _scheduler);
            }
            catch (Exception error)
            {
                _downstreamObserver.OnError(error);
                _scheduledExpiration?.Cancellation.Dispose();
            }

            return Disposable.Empty;
        }

        private void OnSourceNext(IEnumerable<TObject> upstreamItems)
        {
            try
            {
                var unfinishedExpiration = null as ScheduledExpiration?;

                lock (_synchronizationGate)
                {
                    var now = _scheduler.Now;

                    foreach (var item in upstreamItems)
                    {
                        var key = _keySelector.Invoke(item);

                        if ((_limitSizeTo >= 0) && !_downstreamItems.Lookup(key).HasValue)
                        {
                            while (_downstreamItems.Count >= _limitSizeTo)
                                _downstreamItems.Remove(_evictionQueue.Dequeue());

                            _evictionQueue.Enqueue(key);
                        }

                        _downstreamItems.AddOrUpdate(item, key);

                        var lifetime = _expireAfter?.Invoke(item);
                        if (lifetime is TimeSpan lifetimeValue)
                        {
                            var expireAtTicks = (now + Scheduler.Normalize(lifetimeValue)).UtcTicks;
                            var expireAt = new DateTimeOffset(ticks: expireAtTicks - (expireAtTicks % TimeSpan.TicksPerMillisecond), offset: TimeSpan.Zero);

                            _expireAtsByKey[key] = expireAt;

                            var expiration = new Expiration()
                            {
                                ExpireAt = expireAt,
                                Key = key
                            };

                            var insertionIndex = _expirationQueue.BinarySearch(expiration);
                            if (insertionIndex < 0)
                                insertionIndex = ~insertionIndex;

                            _expirationQueue.Insert(
                                index: insertionIndex,
                                item: expiration);
                        }
                        else
                        {
                            _expireAtsByKey.Remove(key);
                        }
                    }

                    unfinishedExpiration = TryBeginSchedulingExpiration();

                    TryPublishDownstreamChanges();
                }

                if (unfinishedExpiration is ScheduledExpiration unfinishedExpirationValue)
                    FinishSchedulingExpiration(unfinishedExpirationValue, _scheduler);
            }
            catch (Exception error)
            {
                _downstreamObserver.OnError(error);
                _scheduledExpiration?.Cancellation.Dispose();
            }
        }

        private void OnSourceCompleted()
        {
            lock (_synchronizationGate)
            {
                _hasSourceCompleted = true;

                TryPublishCompletion();
            }
        }

        // This method must NOT be invoked under the umbrella of _synchronizationGate,
        // as some IScheduler implementations perform locking internally, which can result in deadlocking if we invoke the scheduler within our own lock.
        //
        // Additionally, some IScheduler implementations can invoke actions synchronously,
        // so it's important that scheduler invocation is only performed AFTER downstream changes have been processed.
        // Otherwise, downstream notifications can end up published out-of-order.
        private void FinishSchedulingExpiration(
                ScheduledExpiration unfinishedExpiration,
                IScheduler scheduler)
            => unfinishedExpiration.Cancellation.Disposable = scheduler.Schedule(
                state: unfinishedExpiration.Expiration,
                dueTime: unfinishedExpiration.Expiration.ExpireAt,
                action: OnScheduledExpirationInvoked);

        private void TryPublishCompletion()
        {
            // There needs to be no possibility of a new changeset being emitted before we can call the stream complete.
            if (_hasInitialized && _hasSourceCompleted && (_expirationQueue.Count is 0))
                _downstreamObserver.OnCompleted();
        }

        private void TryPublishDownstreamChanges()
        {
            var downstreamChanges = _downstreamItems.CaptureChanges();
            // Generally, we don't want to emit empty changesets, except if it would be the initial one.
            if ((downstreamChanges.Count is not 0) || !_hasInitialized)
            {
                _downstreamObserver.OnNext(downstreamChanges);
                _hasInitialized = true;
            }
        }

        private ScheduledExpiration? TryBeginSchedulingExpiration()
        {
            // If there's no expirations currently queued up, we don't need to schedule anything.
            if (_expirationQueue.Count is 0)
                return null;

            // If the next expiration in the queue matches what's already scheduled, we don't need to schedule anything.
            var nextExpiration = _expirationQueue[0];
            if ((_scheduledExpiration is ScheduledExpiration scheduledExpiration) && (nextExpiration == scheduledExpiration.Expiration))
                return null;

            // If we made it here, we need to schedule a new expiration action.
            // We can't actually invoke the scheduler here (underneath our synchronization lock) without risking deadlocks,
            // but we do need to start tracking it now, so that said tracking remains synchronized by said lock.
            _scheduledExpiration?.Cancellation.Dispose();
            _scheduledExpiration = new()
            {
                Cancellation = new(),
                Expiration = nextExpiration
            };

            return _scheduledExpiration;
        }
    }

    private readonly struct ScheduledExpiration
    {
        public required SingleAssignmentDisposable Cancellation { get; init; }

        public required Expiration Expiration { get; init; }
    }

    private readonly record struct Expiration
        : IComparable<Expiration>
    {
        public required DateTimeOffset ExpireAt { get; init; }

        public required TKey Key { get; init; }

        public int CompareTo(Expiration other)
            => ExpireAt.CompareTo(other.ExpireAt);
    }
}
