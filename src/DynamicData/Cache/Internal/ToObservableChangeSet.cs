// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ToObservableChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal static class ToObservableChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <param name="expireAfter">The expireAfter value.</param>
    /// <param name="limitSizeTo">The limitSizeTo value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <param name="expireAfter">The expireAfter value.</param>
    /// <param name="limitSizeTo">The limitSizeTo value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Provides members for the Subscription class.
/// </summary>
private sealed class Subscription
        : IDisposable
    {
        /// <summary>
        /// The _downstreamItems field.
        /// </summary>
        private readonly ChangeAwareCache<TObject, TKey> _downstreamItems;

        /// <summary>
        /// The _downstreamObserver field.
        /// </summary>
        private readonly IObserver<IChangeSet<TObject, TKey>> _downstreamObserver;

        /// <summary>
        /// The _evictionQueue field.
        /// </summary>
        private readonly Queue<TKey> _evictionQueue;

        /// <summary>
        /// The _expirationQueue field.
        /// </summary>
        private readonly List<Expiration> _expirationQueue;

        /// <summary>
        /// The _expireAfter field.
        /// </summary>
        private readonly Func<TObject, TimeSpan?>? _expireAfter;

        /// <summary>
        /// The _expireAtsByKey field.
        /// </summary>
        private readonly Dictionary<TKey, DateTimeOffset> _expireAtsByKey;

        /// <summary>
        /// The _keySelector field.
        /// </summary>
        private readonly Func<TObject, TKey> _keySelector;

        /// <summary>
        /// The _limitSizeTo field.
        /// </summary>
        private readonly int _limitSizeTo;

        /// <summary>
        /// The _scheduler field.
        /// </summary>
        private readonly IScheduler _scheduler;

        /// <summary>
        /// The _sourceSubscription field.
        /// </summary>
        private readonly IDisposable _sourceSubscription;

        /// <summary>
        /// The _synchronizationGate field.
        /// </summary>
        private readonly Lock _synchronizationGate;

        /// <summary>
        /// The _hasInitialized field.
        /// </summary>
        private bool _hasInitialized;

        /// <summary>
        /// The _hasSourceCompleted field.
        /// </summary>
        private bool _hasSourceCompleted;

        /// <summary>
        /// The _scheduledExpiration field.
        /// </summary>
        private ScheduledExpiration? _scheduledExpiration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="downstreamObserver">The downstreamObserver value.</param>
        /// <param name="expireAfter">The expireAfter value.</param>
        /// <param name="keySelector">The keySelector value.</param>
        /// <param name="limitSizeTo">The limitSizeTo value.</param>
        /// <param name="scheduler">The scheduler value.</param>
        /// <param name="source">The source value.</param>
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
            _expirationQueue = [];
            _expireAfter = expireAfter;
            _expireAtsByKey = [];
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

        /// <summary>
        /// Executes the Dispose operation.
        /// </summary>
        public void Dispose()
        {
            _sourceSubscription.Dispose();
            _scheduledExpiration?.Cancellation.Dispose();
        }

        /// <summary>
        /// Executes the OnScheduledExpirationInvoked operation.
        /// </summary>
        /// <param name="intendedExpiration">The intendedExpiration value.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>
        /// Executes the OnSourceNext operation.
        /// </summary>
        /// <param name="upstreamItems">The upstreamItems value.</param>
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

        /// <summary>
        /// Executes the OnSourceCompleted operation.
        /// </summary>
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

        /// <summary>
        /// Executes the FinishSchedulingExpiration operation.
        /// </summary>
        /// <param name="unfinishedExpiration">The unfinishedExpiration value.</param>
        /// <param name="scheduler">The scheduler value.</param>
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

        /// <summary>
        /// Executes the TryPublishCompletion operation.
        /// </summary>
        private void TryPublishCompletion()
        {
            // There needs to be no possibility of a new changeset being emitted before we can call the stream complete.
            if (_hasInitialized && _hasSourceCompleted && (_expirationQueue.Count is 0))
                _downstreamObserver.OnCompleted();
        }

        /// <summary>
        /// Executes the TryPublishDownstreamChanges operation.
        /// </summary>
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

        /// <summary>
        /// Executes the TryBeginSchedulingExpiration operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Represents the ScheduledExpiration value.
/// </summary>
private readonly struct ScheduledExpiration
    {
        /// <summary>
        /// Gets or sets the Cancellation value.
        /// </summary>
        public required SingleAssignmentDisposable Cancellation { get; init; }

        /// <summary>
        /// Gets or sets the Expiration value.
        /// </summary>
        public required Expiration Expiration { get; init; }
    }

/// <summary>
/// Represents the Expiration record.
/// </summary>
private readonly record struct Expiration
        : IComparable<Expiration>
    {
        /// <summary>
        /// Gets or sets the ExpireAt value.
        /// </summary>
        public required DateTimeOffset ExpireAt { get; init; }

        /// <summary>
        /// Gets or sets the Key value.
        /// </summary>
        public required TKey Key { get; init; }

        /// <summary>
        /// Executes the CompareTo operation.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result of the operation.</returns>
        public int CompareTo(Expiration other)
            => ExpireAt.CompareTo(other.ExpireAt);
    }
}
