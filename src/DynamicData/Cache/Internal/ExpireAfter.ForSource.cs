// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ExpireAfter class.
/// </summary>
internal static partial class ExpireAfter
{
/// <summary>
/// Provides members for the ForSource class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
public static class ForSource<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        /// <summary>
        /// Executes the Create operation.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="timeSelector">The timeSelector value.</param>
        /// <param name="pollingInterval">The pollingInterval value.</param>
        /// <param name="scheduler">The scheduler value.</param>
        /// <returns>The result of the operation.</returns>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> Create(
            ISourceCache<TObject, TKey> source,
            Func<TObject, TimeSpan?> timeSelector,
            TimeSpan? pollingInterval = null,
            IScheduler? scheduler = null)
        {
            ArgumentExceptionHelper.ThrowIfNull(source);
            ArgumentExceptionHelper.ThrowIfNull(timeSelector);

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

/// <summary>
/// Provides members for the SubscriptionBase class.
/// </summary>
private abstract class SubscriptionBase
            : IDisposable
        {
            /// <summary>
            /// The _expirationDueTimesByKey field.
            /// </summary>
            private readonly Dictionary<TKey, DateTimeOffset> _expirationDueTimesByKey;

            /// <summary>
            /// The _observer field.
            /// </summary>
            private readonly IObserver<IEnumerable<KeyValuePair<TKey, TObject>>> _observer;

            /// <summary>
            /// The _onEditingSource field.
            /// </summary>
            private readonly Action<ISourceUpdater<TObject, TKey>> _onEditingSource;

            /// <summary>
            /// The _proposedExpirationsQueue field.
            /// </summary>
            private readonly List<ProposedExpiration> _proposedExpirationsQueue;

            /// <summary>
            /// The _removedItemsBuffer field.
            /// </summary>
            private readonly List<KeyValuePair<TKey, TObject>> _removedItemsBuffer;

            /// <summary>
            /// The _scheduler field.
            /// </summary>
            private readonly IScheduler _scheduler;

            /// <summary>
            /// The _source field.
            /// </summary>
            private readonly ISourceCache<TObject, TKey> _source;

            /// <summary>
            /// The _sourceSubscription field.
            /// </summary>
            private readonly IDisposable _sourceSubscription;

            /// <summary>
            /// The _timeSelector field.
            /// </summary>
            private readonly Func<TObject, TimeSpan?> _timeSelector;

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

                _expirationDueTimesByKey = [];
                _proposedExpirationsQueue = [];
                _removedItemsBuffer = [];

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
                => _expirationDueTimesByKey;

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
                => _proposedExpirationsQueue.Count is 0
                    ? null
                    : _proposedExpirationsQueue[0].DueTime;

            /// <summary>
            /// Executes the OnExpirationsManaged operation.
            /// </summary>
            /// <param name="dueTime">The dueTime value.</param>
            protected abstract void OnExpirationsManaged(DateTimeOffset dueTime);

            /// <summary>
            /// Executes the ClearExpiration operation.
            /// </summary>
            /// <param name="key">The key value.</param>
            private void ClearExpiration(TKey key)
                // This is what puts the "proposed" in _proposedExpirationsQueue.
                // Finding the position of the item to remove from the queue would be O(log n), at best,
                // so just leave it and flush it later during normal processing of the queue.
                => _expirationDueTimesByKey.Remove(key);

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
                //  - It eliminates the possibility of _itemStatesByKey and other internal state becoming out-of-sync with _source, by effectively locking _source.
                //  - It eliminates a rare deadlock that I honestly can't fully explain, but was able to reproduce reliably with few hundred iterations of the ThreadPoolSchedulerIsUsedWithoutPolling_ExpirationIsThreadSafe test.
                _source.Edit(_onEditingSource);
            }

            /// <summary>
            /// Executes the OnEditingSource operation.
            /// </summary>
            /// <param name="updater">The updater value.</param>
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

                            // The item may have been removed or updated by another thread between when
                            // this expiration was scheduled and when it fired. Check that the item is
                            // still present and still has an expiration before removing it.
                            var lookup = updater.Lookup(proposedExpiration.Key);
                            if (lookup.HasValue && _timeSelector.Invoke(lookup.Value) is not null)
                            {
                                _removedItemsBuffer.Add(new(
                                    key: proposedExpiration.Key,
                                    value: lookup.Value));

                                updater.RemoveKey(proposedExpiration.Key);
                            }
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

            /// <summary>
            /// Executes the OnExpirationsChanged operation.
            /// </summary>
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
                                state: new WeakReference<SubscriptionBase>(this),
                                dueTime: nextManagementDueTime,
                                action: static (_, thisReference) =>
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
                                        haveExpirationsChanged |= TrySetExpiration(
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

            /// <summary>
            /// Executes the TryCancelNextScheduledManagement operation.
            /// </summary>
            private void TryCancelNextScheduledManagement()
            {
                _nextScheduledManagement?.Cancellation.Dispose();
                _nextScheduledManagement = null;
            }

            /// <summary>
            /// Executes the TrySetExpiration operation.
            /// </summary>
            /// <param name="key">The key value.</param>
            /// <param name="dueTime">The dueTime value.</param>
            /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Represents the ProposedExpiration value.
/// </summary>
private readonly struct ProposedExpiration
            {
                /// <summary>
                /// Gets or sets the DueTime value.
                /// </summary>
                public required DateTimeOffset DueTime { get; init; }

                /// <summary>
                /// Gets or sets the Key value.
                /// </summary>
                public required TKey Key { get; init; }
            }

/// <summary>
/// Represents the ScheduledManagement value.
/// </summary>
private readonly struct ScheduledManagement
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
/// <param name="observer">The observer value.</param>
/// <param name="scheduler">The scheduler value.</param>
/// <param name="source">The source value.</param>
/// <param name="timeSelector">The timeSelector value.</param>
private sealed class OnDemandSubscription(
                IObserver<IEnumerable<KeyValuePair<TKey, TObject>>> observer,
                IScheduler? scheduler,
                ISourceCache<TObject, TKey> source,
                Func<TObject, TimeSpan?> timeSelector)
                        : SubscriptionBase(
                observer,
                scheduler,
                source,
                timeSelector)
        {
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

            /// <summary>
            /// Executes the GetNextManagementDueTime operation.
            /// </summary>
            /// <returns>The result of the operation.</returns>
            protected override DateTimeOffset? GetNextManagementDueTime()
            {
                var now = Scheduler.Now;
                var nextDueTime = _lastManagementDueTime + _pollingInterval;

                // Throttle down the polling frequency if polls are taking longer than the ideal interval.
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
}
