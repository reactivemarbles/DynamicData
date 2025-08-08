// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.List.Internal;

internal static class ToObservableChangeSet<TObject>
    where TObject : notnull
{
    public static IObservable<IChangeSet<TObject>> Create(
        IObservable<TObject> source,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject>>(downstreamObserver =>
        {
            var buffer = new TObject[1];

            return Create(
                    source: source
                        .Select(item =>
                        {
                            buffer[0] = item;
                            return buffer;
                        }),
                    expireAfter: expireAfter,
                    limitSizeTo: limitSizeTo,
                    scheduler: scheduler)
                .SubscribeSafe(downstreamObserver);
        });
    }

    public static IObservable<IChangeSet<TObject>> Create(
        IObservable<IEnumerable<TObject>> source,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject>>(downstreamObserver => new Subscription(
            downstreamObserver: downstreamObserver,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler,
            source: source));
    }

    private sealed class Subscription
        : IDisposable
    {
        private readonly ChangeAwareList<TObject> _downstreamItems;
        private readonly IObserver<IChangeSet<TObject>> _downstreamObserver;
        private readonly Func<TObject, TimeSpan?>? _expireAfter;
        private readonly List<Expiration> _expirationQueue;
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
            IObserver<IChangeSet<TObject>> downstreamObserver,
            Func<TObject, TimeSpan?>? expireAfter,
            int limitSizeTo,
            IScheduler? scheduler,
            IObservable<IEnumerable<TObject>> source)
        {
            _downstreamItems = new();
            _downstreamObserver = downstreamObserver;
            _expirationQueue = new();
            _expireAfter = expireAfter;
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

        private IDisposable OnScheduledExpirationInvoked(Expiration intendedExpiration)
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
                    for (var i = 0; i < _expirationQueue.Count; ++i)
                    {
                        var expiration = _expirationQueue[i];

                        // Inaccuracies in real-world scheduler timers make it possible for an item to be invoked slightly before its due time,
                        // so we're going to expire based on the timestamp of the "intended" expiration that this was scheduled for.
                        if (expiration.ExpireAt > intendedExpiration.ExpireAt)
                            break;

                        ++processedExpirationCount;

                        _downstreamItems.RemoveAt(expiration.Index);

                        // Adjust indexes for all remaining items in the queue.
                        for (var j = i + 1; j < _expirationQueue.Count; ++j)
                        {
                            var futureExpiration = _expirationQueue[j];
                            if (futureExpiration.Index > expiration.Index)
                            {
                                _expirationQueue[j] = futureExpiration with
                                {
                                    Index = futureExpiration.Index - 1
                                };
                            }
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
                        if (_limitSizeTo >= 0)
                        {
                            while (_downstreamItems.Count >= _limitSizeTo)
                                _downstreamItems.RemoveAt(0);

                            // Update indexes within the expiration queue, to keep them in-sync with _downstreamItems
                            for (var i = 0; i < _expirationQueue.Count;)
                            {
                                if (_expirationQueue[i].Index == 0)
                                {
                                    _expirationQueue.RemoveAt(i);
                                    continue;
                                }

                                var expiration = _expirationQueue[i];
                                _expirationQueue[i] = expiration with
                                {
                                    Index = expiration.Index - 1
                                };

                                ++i;
                            }

                            // Also, update there's an index to be possibly updated within tracking for the next scheduled expiration
                            if (_scheduledExpiration is ScheduledExpiration scheduledExpiration)
                            {
                                // If the next expiration is for the item we just removed, cancel it.
                                if (scheduledExpiration.Expiration.Index == 0)
                                {
                                    scheduledExpiration.Cancellation.Dispose();
                                    _scheduledExpiration = null;
                                }
                                // Otherwise, just adjust the index
                                else
                                {
                                    _scheduledExpiration = scheduledExpiration with
                                    {
                                        Expiration = scheduledExpiration.Expiration with
                                        {
                                            Index = scheduledExpiration.Expiration.Index - 1
                                        }
                                    };
                                }
                            }
                        }

                        _downstreamItems.Add(item);

                        var lifetime = _expireAfter?.Invoke(item);
                        if (lifetime is TimeSpan lifetimeValue)
                        {
                            var expireAtTicks = (now + Scheduler.Normalize(lifetimeValue)).UtcTicks;
                            var expireAt = new DateTimeOffset(ticks: expireAtTicks - (expireAtTicks % TimeSpan.TicksPerMillisecond), offset: TimeSpan.Zero);

                            var expiration = new Expiration()
                            {
                                ExpireAt = expireAt,
                                Index = _downstreamItems.Count - 1
                            };

                            var insertionIndex = _expirationQueue.BinarySearch(expiration);
                            if (insertionIndex < 0)
                                insertionIndex = ~insertionIndex;

                            _expirationQueue.Insert(
                                index: insertionIndex,
                                item: expiration);
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
                state: (
                    thisReference: new WeakReference<Subscription>(this),
                    expiration: unfinishedExpiration.Expiration),
                dueTime: unfinishedExpiration.Expiration.ExpireAt,
                action: static (_, state) =>
                {
                    // Most schedulers won't clear scheduled actions upon cancellation, they'll wait until they were supposed to occur.
                    // A WeakReference here prevents the whole subscription from memory leaking
                    // Refer to https://github.com/reactivemarbles/DynamicData/issues/1025
                    if (state.thisReference.TryGetTarget(out var @this))
                        @this.OnScheduledExpirationInvoked(state.expiration);

                    return Disposable.Empty;
                });

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

        public required int Index { get; init; }

        public int CompareTo(Expiration other)
            => ExpireAt.CompareTo(other.ExpireAt);
    }
}
