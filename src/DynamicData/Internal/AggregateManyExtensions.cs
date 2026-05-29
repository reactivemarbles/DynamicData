// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides the <see cref="AggregateMany{TSource, TKey, TInner, TResult}"/> operator: a delegate-driven entry point
/// that subscribes to a keyed source changeset, manages per-key inner subscriptions, and coalesces source and inner
/// notifications into a single downstream emission per drain cycle of an internal <see cref="SharedDeliveryQueue"/>.
/// </summary>
internal static class AggregateManyExtensions
{
    /// <summary>
    /// Aggregates a keyed source changeset and a dynamic set of per-key inner observables into a single result stream.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream by <paramref name="emit"/>.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="onSource">
    /// Invoked for each source changeset, paired with a <c>track</c> callback that registers,
    /// replaces, or removes the inner observable for a key. Pass a non-<see langword="null"/>
    /// observable to register or replace; pass <see langword="null"/> to remove. The callback
    /// routes the inner observable through the shared delivery queue so callers do not need
    /// to synchronize it themselves.
    /// </param>
    /// <param name="onInner">Invoked for each value emitted by a tracked inner observable, paired with its key.</param>
    /// <param name="emit">Invoked once per drain cycle to flush the aggregated state to the observer.</param>
    /// <returns>An observable that aggregates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> AggregateMany<TSource, TKey, TInner, TResult>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Action<IChangeSet<TSource, TKey>, Action<TKey, IObservable<TInner>?>> onSource,
            Action<TInner, TKey> onInner,
            Action<IObserver<TResult>> emit)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        Observable.Create<TResult>(observer => new Subscription<TSource, TKey, TInner, TResult>(source, observer, onSource, onInner, emit));

    private sealed class Subscription<TSource, TKey, TInner, TResult> : IDisposable
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
    {
        private readonly KeyedDisposable<TKey> _innerSubscriptions = new();
        private readonly SingleAssignmentDisposable _sourceSubscription = new();
        private readonly SharedDeliveryQueue _queue;
        private readonly IObserver<TResult> _observer;
        private readonly Action<IChangeSet<TSource, TKey>, Action<TKey, IObservable<TInner>?>> _onSource;
        private readonly Action<TInner, TKey> _onInner;
        private readonly Action<IObserver<TResult>> _emit;
        private int _subscriptionCounter = 1;
        private bool _isCompleted;
        private bool _hasTerminated;
        private bool _disposed;

        public Subscription(
                IObservable<IChangeSet<TSource, TKey>> source,
                IObserver<TResult> observer,
                Action<IChangeSet<TSource, TKey>, Action<TKey, IObservable<TInner>?>> onSource,
                Action<TInner, TKey> onInner,
                Action<IObserver<TResult>> emit)
        {
            _observer = observer;
            _onSource = onSource;
            _onInner = onInner;
            _emit = emit;
            _queue = new SharedDeliveryQueue(onDrainComplete: OnDrainComplete);

            _sourceSubscription.Disposable = source
                .SynchronizeSafe(_queue)
                .SubscribeSafe(
                    onNext: changes => _onSource(changes, Track),
                    onError: TerminalError,
                    onCompleted: DecrementSubscriptionCount);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.Dispose();
            _sourceSubscription.Dispose();
            _innerSubscriptions.Dispose();
        }

        private void Track(TKey key, IObservable<TInner>? observable)
        {
            if (observable is null)
            {
                _innerSubscriptions.Remove(key);
                return;
            }

            // Increment before adding so the OnCompleted callback that fires when the previous subscription
            // for this key is disposed does not race the counter down to zero and signal premature termination.
            Interlocked.Increment(ref _subscriptionCounter);

            var container = _innerSubscriptions.Add(key, new SingleAssignmentDisposable());

            // Finally(DecrementSubscriptionCount) fires on completion, error, AND disposal, so the counter
            // always decrements. The onCompleted callback only fires on normal completion, so an inner
            // subscription disposed by Track replacing it (or by Dispose) does not trigger Remove from inside.
            container.Disposable = observable
                .SynchronizeSafe(_queue)
                .Finally(DecrementSubscriptionCount)
                .SubscribeSafe(
                    onNext: value => _onInner(value, key),
                    onError: TerminalError,
                    onCompleted: () => _innerSubscriptions.Remove(key));
        }

        private void OnDrainComplete()
        {
            _emit(_observer);

            if (Volatile.Read(ref _isCompleted) && !_hasTerminated)
            {
                _hasTerminated = true;
                _observer.OnCompleted();
            }
        }

        private void TerminalError(Exception error)
        {
            _hasTerminated = true;
            _observer.OnError(error);
        }

        private void DecrementSubscriptionCount()
        {
            if (Interlocked.Decrement(ref _subscriptionCounter) == 0)
            {
                Volatile.Write(ref _isCompleted, true);
            }

            Debug.Assert(_subscriptionCounter >= 0, "Should never be negative");
        }
    }
}
