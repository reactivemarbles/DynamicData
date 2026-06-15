// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.List.Internal;

internal sealed class ExpireAfter<T>
    where T : notnull
{
    public static IObservable<IEnumerable<T>> Create(
        ISourceList<T> source,
        Func<T, TimeSpan?> timeSelector,
        TimeSpan? pollingInterval = null,
        IScheduler? scheduler = null)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        timeSelector.ThrowArgumentNullExceptionIfNull(nameof(timeSelector));

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

    private abstract class SubscriptionBase
        : IDisposable
    {
        // Shadow of the source maintained from OnSourceNext, carrying both the item and its
        // expiration time per occurrence. Item identity (by value) is used during expiration
        // because the shadow may be stale relative to the source: the new SourceList uses
        // queue-based delivery, so notifications drain after the producing Edit releases its
        // lock. A concurrent ManageExpirations cannot rely on shadow indices matching source
        // indices; matching by item value via updater.IndexOf tolerates this divergence and
        // simply skips items that were already removed externally.
        private readonly List<ItemEntry> _shadow;
        private readonly List<int> _expiringShadowIndexesBuffer;
        private readonly IObserver<IEnumerable<T>> _observer;
        private readonly Action<IExtendedList<T>> _onEditingSource;
        private readonly IScheduler _scheduler;
        private readonly ISourceList<T> _source;
        private readonly IDisposable _sourceSubscription;
        private readonly Func<T, TimeSpan?> _timeSelector;

        private bool _hasSourceCompleted;
        private ScheduledManagement? _nextScheduledManagement;

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

            _shadow = new();
            _expiringShadowIndexesBuffer = new();

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
            => _shadow;

        protected abstract DateTimeOffset? GetNextManagementDueTime();

        protected DateTimeOffset? GetNextProposedExpirationDueTime()
        {
            var result = null as DateTimeOffset?;

            foreach (var entry in _shadow)
            {
                if ((entry.DueTime is { } value) && ((result is null) || (value < result)))
                    result = value;
            }

            return result;
        }

        protected abstract void OnExpirationsManaged(DateTimeOffset dueTime);

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

        private void OnEditingSource(IExtendedList<T> updater)
        {
            lock (SynchronizationGate)
            {
                // The scheduler only promises "best effort" to cancel scheduled operations, so we need to make sure.
                if (_nextScheduledManagement is not { } thisScheduledManagement)
                    return;

                _nextScheduledManagement = null;

                var now = Scheduler.Now;

                // We are NOT updating our shadow here, except to mark items as no longer needing to expire.
                // Once source.Edit() returns, the resulting changeset will reach OnSourceNext and bring _shadow
                // back into sync. The shadow may have been stale on entry (concurrent edits could be queued
                // behind a pending drain), so we identify removals by item value instead of by shadow index.

                for (var i = 0; i < _shadow.Count; ++i)
                {
                    if ((_shadow[i].DueTime is { } dueTime) && (dueTime <= now))
                    {
                        _expiringShadowIndexesBuffer.Add(i);

                        // Mark as processed so we don't expire the same shadow slot twice if reentered.
                        _shadow[i] = new ItemEntry(_shadow[i].Item, null);
                    }
                }

                if (_expiringShadowIndexesBuffer.Count is not 0)
                {
                    var removedItems = new List<T>(_expiringShadowIndexesBuffer.Count);

                    // Validate the shadow against the live updater all the way up to (and including)
                    // the LAST expiring index. The previous version of this code only checked the
                    // prefix up to the FIRST expiring index, which can falsely declare the shadow
                    // in sync when a concurrent external mutation has moved/replaced an item at a
                    // position between the first and last expiring index. The subsequent reverse-
                    // index removal would then remove an unrelated item. If the shadow is stale we
                    // fall back to value-based IndexOf, which may remove a different equal
                    // occurrence than originally scheduled but at least keeps the source consistent
                    // with what subscribers see.
                    var lastExpiringShadowIdx = _expiringShadowIndexesBuffer[_expiringShadowIndexesBuffer.Count - 1];
                    var shadowInSync = _shadow.Count == updater.Count;
                    if (shadowInSync)
                    {
                        for (var i = 0; i <= lastExpiringShadowIdx && i < updater.Count; ++i)
                        {
                            if (!EqualityComparer<T>.Default.Equals(_shadow[i].Item, updater[i]))
                            {
                                shadowInSync = false;
                                break;
                            }
                        }
                    }

                    if (shadowInSync)
                    {
                        // Index-based removal in REVERSE shadow order so earlier indices stay
                        // valid as we remove later items. This matches the legacy behaviour and
                        // correctly distinguishes between equal duplicate occurrences with
                        // different expiration times.
                        for (var i = _expiringShadowIndexesBuffer.Count - 1; i >= 0; --i)
                        {
                            var shadowIdx = _expiringShadowIndexesBuffer[i];
                            removedItems.Add(updater[shadowIdx]);
                            updater.RemoveAt(shadowIdx);
                        }
                    }
                    else
                    {
                        // Shadow is stale (concurrent external mutation has been queued but not
                        // yet delivered to OnSourceNext). Fall back to value-based search.
                        // Iterate shadow forward; for each expiring entry remove the first
                        // matching live occurrence. Silently skip items that have already been
                        // removed externally; the pending OnSourceNext will reconcile the shadow.
                        for (var i = 0; i < _expiringShadowIndexesBuffer.Count; ++i)
                        {
                            var item = _shadow[_expiringShadowIndexesBuffer[i]].Item;
                            var idx = updater.IndexOf(item);
                            if (idx >= 0)
                            {
                                updater.RemoveAt(idx);
                                removedItems.Add(item);
                            }
                        }
                    }

                    if (removedItems.Count > 0)
                        _observer.OnNext(removedItems);

                    _expiringShadowIndexesBuffer.Clear();
                }

                OnExpirationsManaged(thisScheduledManagement.DueTime);

                // We just changed due times, so run cleanup and management scheduling.
                OnExpirationDueTimesChanged();
            }
        }

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

                                _shadow.Insert(
                                    index: change.Item.CurrentIndex,
                                    item: new ItemEntry(change.Item.Current, dueTime));

                                haveExpirationDueTimesChanged |= dueTime is not null;
                            }
                            break;

                        case ListChangeReason.AddRange:
                            {
                                _shadow.EnsureCapacity(_shadow.Count + change.Range.Count);

                                var itemIndex = change.Range.Index;
                                foreach (var item in change.Range)
                                {
                                    var dueTime = now + _timeSelector.Invoke(item);

                                    _shadow.Insert(
                                        index: itemIndex,
                                        item: new ItemEntry(item, dueTime));

                                    haveExpirationDueTimesChanged |= dueTime is not null;

                                    ++itemIndex;
                                }
                            }
                            break;

                        case ListChangeReason.Clear:
                            foreach (var entry in _shadow)
                            {
                                if (entry.DueTime is not null)
                                {
                                    haveExpirationDueTimesChanged = true;
                                    break;
                                }
                            }

                            _shadow.Clear();
                            break;

                        case ListChangeReason.Moved:
                            {
                                var entry = _shadow[change.Item.PreviousIndex];

                                _shadow.RemoveAt(change.Item.PreviousIndex);
                                _shadow.Insert(
                                    index: change.Item.CurrentIndex,
                                    item: entry);
                            }
                            break;

                        case ListChangeReason.Remove:
                            {
                                if (_shadow[change.Item.CurrentIndex].DueTime is not null)
                                {
                                    haveExpirationDueTimesChanged = true;
                                }

                                _shadow.RemoveAt(change.Item.CurrentIndex);
                            }
                            break;

                        case ListChangeReason.RemoveRange:
                            {
                                var rangeEndIndex = change.Range.Index + change.Range.Count - 1;
                                for (var i = change.Range.Index; i <= rangeEndIndex; ++i)
                                {
                                    if (_shadow[i].DueTime is not null)
                                    {
                                        haveExpirationDueTimesChanged = true;
                                        break;
                                    }
                                }

                                _shadow.RemoveRange(change.Range.Index, change.Range.Count);
                            }
                            break;

                        case ListChangeReason.Replace:
                            {
                                var oldDueTime = _shadow[change.Item.CurrentIndex].DueTime;
                                var newDueTime = now + _timeSelector.Invoke(change.Item.Current);

                                // Ignoring the possibility that the item's index has changed as well, because ISourceList<T> does not allow for this.

                                _shadow[change.Item.CurrentIndex] = new ItemEntry(change.Item.Current, newDueTime);

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

        private void TryCancelNextScheduledManagement()
        {
            _nextScheduledManagement?.Cancellation.Dispose();
            _nextScheduledManagement = null;
        }

        private readonly record struct ScheduledManagement
        {
            public required IDisposable Cancellation { get; init; }

            public required DateTimeOffset DueTime { get; init; }
        }

        private readonly record struct ItemEntry(T Item, DateTimeOffset? DueTime);
    }

    private sealed class OnDemandSubscription
        : SubscriptionBase
    {
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

        protected override DateTimeOffset? GetNextManagementDueTime()
        {
            var now = Scheduler.Now;
            var nextDueTime = _lastManagementDueTime + _pollingInterval;

            // Make sure we don't flood the system with polls, if the processing time of a poll ever exceeds the polling interval.
            return (nextDueTime > now)
                ? nextDueTime
                : now;
        }

        protected override void OnExpirationsManaged(DateTimeOffset dueTime)
            => _lastManagementDueTime = dueTime;
    }
}
