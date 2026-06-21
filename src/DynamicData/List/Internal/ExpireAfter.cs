// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the ExpireAfter class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class ExpireAfter<T>
    where T : notnull
{
    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="timeSelector">The timeSelector value.</param>
    /// <param name="pollingInterval">The pollingInterval value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    /// <returns>The result of the operation.</returns>
    public static IObservable<IEnumerable<T>> Create(
        ISourceList<T> source,
        Func<T, TimeSpan?> timeSelector,
        TimeSpan? pollingInterval = null,
        IScheduler? scheduler = null)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(timeSelector);

        return Observable.Create<IEnumerable<T>>(observer => (pollingInterval is { } pollingIntervalValue)
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

/// <summary>
/// Provides members for the SubscriptionBase class.
/// </summary>
private abstract class SubscriptionBase
        : IDisposable
    {
        /// <summary>
        /// The _expirationDueTimes field.
        /// </summary>
        private readonly List<DateTimeOffset?> _expirationDueTimes;

        /// <summary>
        /// The _expiringIndexesBuffer field.
        /// </summary>
        private readonly List<int> _expiringIndexesBuffer;

        /// <summary>
        /// The _observer field.
        /// </summary>
        private readonly IObserver<IEnumerable<T>> _observer;

        /// <summary>
        /// The _onEditingSource field.
        /// </summary>
        private readonly Action<IExtendedList<T>> _onEditingSource;

        /// <summary>
        /// The _scheduler field.
        /// </summary>
        private readonly IScheduler _scheduler;

        /// <summary>
        /// The _source field.
        /// </summary>
        private readonly ISourceList<T> _source;

        /// <summary>
        /// The _sourceSubscription field.
        /// </summary>
        private readonly IDisposable _sourceSubscription;

        /// <summary>
        /// The _timeSelector field.
        /// </summary>
        private readonly Func<T, TimeSpan?> _timeSelector;

        /// <summary>
        /// The _hasSourceCompleted field.
        /// </summary>
        private bool _hasSourceCompleted;

        /// <summary>
        /// The _nextScheduledManagement field.
        /// </summary>
        private ScheduledManagement? _nextScheduledManagement;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionBase"/> class.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        /// <param name="scheduler">The scheduler value.</param>
        /// <param name="source">The source value.</param>
        /// <param name="timeSelector">The timeSelector value.</param>
        protected SubscriptionBase(
            IObserver<IEnumerable<T>> observer,
            IScheduler? scheduler,
            ISourceList<T> source,
            Func<T, TimeSpan?> timeSelector)
        {
            _observer = observer;
            _source = source;
            _timeSelector = timeSelector;

            _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

            _onEditingSource = OnEditingSource;

            _expirationDueTimes = new();
            _expiringIndexesBuffer = new();

            _sourceSubscription = PrimitivesLinqExtensions.SubscribeSafe(
                source
                    .Connect()
                    // It's important to set this flag outside the context of a lock, because it'll be read outside of lock as well.
                    .Finally(() => _hasSourceCompleted = true)
                    .Synchronize(SynchronizationGate),
                onNext: OnSourceNext,
                onError: OnSourceError,
                onCompleted: OnSourceCompleted);
        }

        /// <summary>
        /// Executes the Dispose operation.
        /// </summary>
        public void Dispose()
        {
            lock (SynchronizationGate)
            {
                _sourceSubscription.Dispose();

                TryCancelNextScheduledManagement();
            }
        }

        /// <summary>
        /// Gets the Scheduler value.
        /// </summary>
        protected IScheduler Scheduler
            => _scheduler;
        // Instead of using a dedicated _synchronizationGate object, we can save an allocation by using any object that is never exposed to public consumers.

        /// <summary>
        /// Gets the SynchronizationGate value.
        /// </summary>
        protected object SynchronizationGate
            => _expirationDueTimes;

        /// <summary>
        /// Executes the GetNextManagementDueTime operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        protected abstract DateTimeOffset? GetNextManagementDueTime();

        /// <summary>
        /// Executes the GetNextProposedExpirationDueTime operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        protected DateTimeOffset? GetNextProposedExpirationDueTime()
        {
            var result = null as DateTimeOffset?;

            foreach (var dueTime in _expirationDueTimes)
            {
                if ((dueTime is { } value) && ((result is null) || (value < result)))
                    result = value;
            }

            return result;
        }

        /// <summary>
        /// Executes the OnExpirationsManaged operation.
        /// </summary>
        /// <param name="dueTime">The dueTime value.</param>
        protected abstract void OnExpirationsManaged(DateTimeOffset dueTime);

        /// <summary>
        /// Executes the ManageExpirations operation.
        /// </summary>
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
            //  - It eliminates the possibility of our internal state/item caches becoming out-of-sync with _source, by effectively locking _source.
            //  - It eliminates a rare deadlock that I honestly can't fully explain, but was able to reproduce reliably with few hundred iterations of the ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe test, on the Cache-equivalent of this operator.
            _source.Edit(_onEditingSource);
        }

        /// <summary>
        /// Executes the OnEditingSource operation.
        /// </summary>
        /// <param name="updater">The updater value.</param>
        private void OnEditingSource(IExtendedList<T> updater)
        {
            lock (SynchronizationGate)
            {
                // The scheduler only promises "best effort" to cancel scheduled operations, so we need to make sure.
                if (_nextScheduledManagement is not { } thisScheduledManagement)
                    return;

                _nextScheduledManagement = null;

                var now = Scheduler.Now;

                // One major note here: we are NOT updating our internal state, except to mark items as no longer needing to expire.
                // Once we're done with the source.Edit() here, it will fire of a changeset for the removals, which will get handled by OnSourceNext(),
                // thus bringing all of our internal state back into sync.

                // Buffer removals, so we can eliminate the need for index adjustments as we update the source
                for (var i = 0; i < _expirationDueTimes.Count; ++i)
                {
                    if ((_expirationDueTimes[i] is { } dueTime) && (dueTime <= now))
                    {
                        _expiringIndexesBuffer.Add(i);

                        // This shouldn't be necessary, but it guarantees we don't accidentally expire an item more than once,
                        // in the event of a race condition or something we haven't predicted.
                        _expirationDueTimes[i] = null;
                    }
                }

                // I'm pretty sure it shouldn't be possible to end up with no removals here, but it costs basically nothing to check.
                if (_expiringIndexesBuffer.Count is not 0)
                {
                    // Processing removals in reverse-index order eliminates the need for us to adjust index of each .RemoveAt() call, as we go.
                    _expiringIndexesBuffer.Sort(static (x, y) => y.CompareTo(x));

                    var removedItems = new T[_expiringIndexesBuffer.Count];
                    for (var i = 0; i < _expiringIndexesBuffer.Count; ++i)
                    {
                        var removedIndex = _expiringIndexesBuffer[i];
                        removedItems[i] = updater[removedIndex];
                        updater.RemoveAt(removedIndex);
                    }

                    _observer.OnNext(removedItems);

                    _expiringIndexesBuffer.Clear();
                }

                OnExpirationsManaged(thisScheduledManagement.DueTime);

                // We just changed due times, so run cleanup and management scheduling.
                OnExpirationDueTimesChanged();
            }
        }

        /// <summary>
        /// Executes the OnExpirationDueTimesChanged operation.
        /// </summary>
        private void OnExpirationDueTimesChanged()
        {
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
                            state: new WeakReference<SubscriptionBase>(this),
                            dueTime: nextManagementDueTime,
                            action: (_, thisReference) =>
                            {
                                // Most schedulers won't clear scheduled actions upon cancellation, they'll wait until they were supposed to occur.
                                // A WeakReference here prevents the whole subscription from memory leaking
                                // Refer to https://github.com/reactivemarbles/DynamicData/issues/1025
                                if (thisReference.TryGetTarget(out var @this))
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

        /// <summary>
        /// Executes the OnSourceCompleted operation.
        /// </summary>
        private void OnSourceCompleted()
        {
            // If the source completes, we can no longer remove items from it, so any pending expirations are moot.
            TryCancelNextScheduledManagement();

            _observer.OnCompleted();
        }

        /// <summary>
        /// Executes the OnSourceError operation.
        /// </summary>
        /// <param name="error">The error value.</param>
        private void OnSourceError(Exception error)
        {
            TryCancelNextScheduledManagement();

            _observer.OnError(error);
        }

        /// <summary>
        /// Executes the OnSourceNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        private void OnSourceNext(IChangeSet<T> changes)
        {
            try
            {
                var now = _scheduler.Now;

                var haveExpirationDueTimesChanged = false;

                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            {
                                var dueTime = now + _timeSelector.Invoke(change.Item.Current);

                                _expirationDueTimes.Insert(
                                    index: change.Item.CurrentIndex,
                                    item: dueTime);

                                haveExpirationDueTimesChanged |= dueTime is not null;
                            }
                            break;

                        case ListChangeReason.AddRange:
                            {
                                _expirationDueTimes.EnsureCapacity(_expirationDueTimes.Count + change.Range.Count);

                                var itemIndex = change.Range.Index;
                                foreach (var item in change.Range)
                                {
                                    var dueTime = now + _timeSelector.Invoke(item);

                                    _expirationDueTimes.Insert(
                                        index: itemIndex,
                                        item: dueTime);

                                    haveExpirationDueTimesChanged |= dueTime is not null;

                                    ++itemIndex;
                                }
                            }
                            break;

                        case ListChangeReason.Clear:
                            foreach (var dueTime in _expirationDueTimes)
                            {
                                if (dueTime is not null)
                                {
                                    haveExpirationDueTimesChanged = true;
                                    break;
                                }
                            }

                            _expirationDueTimes.Clear();
                            break;

                        case ListChangeReason.Moved:
                            {
                                var expirationDueTime = _expirationDueTimes[change.Item.PreviousIndex];

                                _expirationDueTimes.RemoveAt(change.Item.PreviousIndex);
                                _expirationDueTimes.Insert(
                                    index: change.Item.CurrentIndex,
                                    item: expirationDueTime);
                            }
                            break;

                        case ListChangeReason.Remove:
                            {
                                if (_expirationDueTimes[change.Item.CurrentIndex] is not null)
                                {
                                    haveExpirationDueTimesChanged = true;
                                }

                                _expirationDueTimes.RemoveAt(change.Item.CurrentIndex);
                            }
                            break;

                        case ListChangeReason.RemoveRange:
                            {
                                var rangeEndIndex = change.Range.Index + change.Range.Count - 1;
                                for (var i = change.Range.Index; i <= rangeEndIndex; ++i)
                                {
                                    if (_expirationDueTimes[i] is not null)
                                    {
                                        haveExpirationDueTimesChanged = true;
                                        break;
                                    }
                                }

                                _expirationDueTimes.RemoveRange(change.Range.Index, change.Range.Count);
                            }
                            break;

                        case ListChangeReason.Replace:
                            {
                                var oldDueTime = _expirationDueTimes[change.Item.CurrentIndex];
                                var newDueTime = now + _timeSelector.Invoke(change.Item.Current);

                                // Ignoring the possibility that the item's index has changed as well, because ISourceList<T> does not allow for this.

                                _expirationDueTimes[change.Item.CurrentIndex] = newDueTime;

                                haveExpirationDueTimesChanged |= newDueTime != oldDueTime;
                            }
                            break;

                        // Ignoring Refresh changes, since ISourceList<T> doesn't generate them.
                    }
                }

                if (haveExpirationDueTimesChanged)
                    OnExpirationDueTimesChanged();
            }
            catch (Exception error)
            {
                TryCancelNextScheduledManagement();

                _observer.OnError(error);
            }
        }

        /// <summary>
        /// Executes the TryCancelNextScheduledManagement operation.
        /// </summary>
        private void TryCancelNextScheduledManagement()
        {
            _nextScheduledManagement?.Cancellation.Dispose();
            _nextScheduledManagement = null;
        }

/// <summary>
/// Represents the ScheduledManagement record.
/// </summary>
private readonly record struct ScheduledManagement
        {
            /// <summary>
            /// Gets or sets the Cancellation value.
            /// </summary>
            public required IDisposable Cancellation { get; init; }

            /// <summary>
            /// Gets or sets the DueTime value.
            /// </summary>
            public required DateTimeOffset DueTime { get; init; }
        }
    }

/// <summary>
/// Provides members for the OnDemandSubscription class.
/// </summary>
private sealed class OnDemandSubscription
        : SubscriptionBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OnDemandSubscription"/> class.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        /// <param name="scheduler">The scheduler value.</param>
        /// <param name="source">The source value.</param>
        /// <param name="timeSelector">The timeSelector value.</param>
        public OnDemandSubscription(
                IObserver<IEnumerable<T>> observer,
                IScheduler? scheduler,
                ISourceList<T> source,
                Func<T, TimeSpan?> timeSelector)
            : base(
                observer,
                scheduler,
                source,
                timeSelector)
        {
        }

        /// <summary>
        /// Executes the GetNextManagementDueTime operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        protected override DateTimeOffset? GetNextManagementDueTime()
            => GetNextProposedExpirationDueTime();

        /// <summary>
        /// Executes the OnExpirationsManaged operation.
        /// </summary>
        /// <param name="dueTime">The dueTime value.</param>
        protected override void OnExpirationsManaged(DateTimeOffset dueTime)
        {
        }
    }

/// <summary>
/// Provides members for the PollingSubscription class.
/// </summary>
private sealed class PollingSubscription
        : SubscriptionBase
    {
        /// <summary>
        /// The _pollingInterval field.
        /// </summary>
        private readonly TimeSpan _pollingInterval;

        /// <summary>
        /// The _lastManagementDueTime field.
        /// </summary>
        private DateTimeOffset _lastManagementDueTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="PollingSubscription"/> class.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        /// <param name="pollingInterval">The pollingInterval value.</param>
        /// <param name="scheduler">The scheduler value.</param>
        /// <param name="source">The source value.</param>
        /// <param name="timeSelector">The timeSelector value.</param>
        public PollingSubscription(
                IObserver<IEnumerable<T>> observer,
                TimeSpan pollingInterval,
                IScheduler? scheduler,
                ISourceList<T> source,
                Func<T, TimeSpan?> timeSelector)
            : base(
                observer,
                scheduler,
                source,
                timeSelector)
        {
            _pollingInterval = pollingInterval;

            _lastManagementDueTime = Scheduler.Now;
        }

        /// <summary>
        /// Executes the GetNextManagementDueTime operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        protected override DateTimeOffset? GetNextManagementDueTime()
        {
            var now = Scheduler.Now;
            var nextDueTime = _lastManagementDueTime + _pollingInterval;

            // Make sure we don't flood the system with polls, if the processing time of a poll ever exceeds the polling interval.
            return (nextDueTime > now)
                ? nextDueTime
                : now;
        }

        /// <summary>
        /// Executes the OnExpirationsManaged operation.
        /// </summary>
        /// <param name="dueTime">The dueTime value.</param>
        protected override void OnExpirationsManaged(DateTimeOffset dueTime)
            => _lastManagementDueTime = dueTime;
    }
}
