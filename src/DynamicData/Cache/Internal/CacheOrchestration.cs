// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Drives an <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/> against a source
/// changeset. <see cref="Run"/> returns an <see cref="IObservable{TResult}"/> that constructs a
/// fresh per-subscription <see cref="OrchestratorContext"/> on each subscribe, so all per-subscription
/// state owned by the <see cref="OrchestratorContext"/> is recreated on every subscribe. The
/// orchestrator itself is constructed by the supplied <paramref name="factory"/>, which receives
/// the per-subscription context and emitter; this guarantees a fresh orchestrator instance per
/// subscriber and removes the need for a separate <c>Initialize</c> hook.
/// </summary>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream.</typeparam>
/// <typeparam name="TOrch">Concrete orchestrator type returned by the factory. Generic-typed so
/// dispatch sites devirtualize.</typeparam>
/// <param name="source">The keyed source changeset stream.</param>
/// <param name="factory">Builds the per-subscription orchestrator from its runtime context and emitter.</param>
internal sealed class CacheOrchestration<TSource, TKey, TInner, TResult, TOrch>(
        IObservable<IChangeSet<TSource, TKey>> source,
        Func<ICacheOrchestratorContext<TKey, TInner>, IObserver<TResult>, TOrch> factory)
    where TSource : notnull
    where TKey : notnull
    where TInner : notnull
    where TOrch : ICacheOrchestrator<TSource, TKey, TInner, TResult>
{
    public IObservable<TResult> Run() => Observable.Create<TResult>(observer => new OrchestratorContext(source, observer, factory));

    private sealed class OrchestratorContext : ICacheOrchestratorContext<TKey, TInner>, IDisposable
    {
        private readonly KeyedDisposable<TKey> _innerSubscriptions = new();
        private readonly SingleAssignmentDisposable _sourceSubscription = new();
        private readonly SharedDeliveryQueue _queue;
        private readonly DeliverySubQueue<TResult> _emitter;
        private readonly TOrch _orchestrator = default!;
        private int _subscriptionCounter = 1;   // Includes the source subscription, so starts at 1 and not 0.
        private int _completionEmitted;
        private bool _disposed;

        public OrchestratorContext(
                IObservable<IChangeSet<TSource, TKey>> source,
                IObserver<TResult> observer,
                Func<ICacheOrchestratorContext<TKey, TInner>, IObserver<TResult>, TOrch> factory)
        {
            _queue = new SharedDeliveryQueue(onDrainComplete: OnDrainComplete);

            // Wrap construction from the emitter sub-queue allocation through the source subscription
            // so any throw on the way up releases everything we've allocated so far. Without this, an
            // exception from CreateQueue, the factory, or the source subscribe leaks the queue/emitter/
            // orchestrator/source-subscription because the ctor never completes and Dispose never runs.
            try
            {
                // Create the emitter sub-queue first (lowest index, drains last LIFO) so source-triggered
                // sync inner emissions (higher index) deliver first and any orchestrator emit lands on the
                // emitter after that work has settled.
                _emitter = _queue.CreateQueue(observer);

                _orchestrator = factory(this, _emitter);
                Debug.Assert(_orchestrator is not null, "Factory must not return null");

                _sourceSubscription.Disposable = source
                    .SynchronizeSafe(_queue)
                    .SubscribeSafe(
                        onNext: OnSourceChangeSet,
                        onError: _emitter.OnError,
                        onCompleted: DecrementSubscriptionCount);
            }
            catch
            {
                _sourceSubscription.Dispose();
                _innerSubscriptions.Dispose();
                _queue.Dispose();
                _emitter?.Dispose();
                (_orchestrator as IDisposable)?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Stop incoming work first so source/inner pumps cannot keep firing into a terminating
            // queue. The queue itself is disposed afterwards to drain (or terminate) cleanly.
            _sourceSubscription.Dispose();
            _innerSubscriptions.Dispose();
            _queue.Dispose();
            _emitter.Dispose();
            (_orchestrator as IDisposable)?.Dispose();
        }

        public void Track(TKey key, IObservable<TInner> observable)
        {
            Debug.Assert(observable is not null, "Use Untrack(key) to remove a tracked subscription");

            // Increment before adding so the OnCompleted callback that fires when the previous subscription
            // for this key is disposed does not race the counter down to zero and signal premature termination.
            Interlocked.Increment(ref _subscriptionCounter);

            var container = _innerSubscriptions.Add(key, new SingleAssignmentDisposable());

            // Finally(DecrementSubscriptionCount) fires on completion, error, AND disposal, so the counter
            // always decrements. The onCompleted callback only fires on normal completion, so an inner
            // subscription disposed by Track replacing it (or by Dispose) does not trigger Remove from inside.
            container.Disposable = observable!
                .SynchronizeSafe(_queue)
                .Finally(DecrementSubscriptionCount)
                .SubscribeSafe(
                    onNext: value => OnInner(value, key),
                    onError: _emitter.OnError,
                    onCompleted: () => _innerSubscriptions.Remove(key));
        }

        public void Untrack(TKey key) => _innerSubscriptions.Remove(key);

        public IObservable<T> Serialize<T>(IObservable<T> observable) => observable.SynchronizeSafe(_queue);

        private void OnSourceChangeSet(IChangeSet<TSource, TKey> changes)
        {
            try
            {
                _orchestrator.OnSourceChangeSet(changes);
            }
            catch (Exception error)
            {
                _emitter.OnError(error);
            }
        }

        private void OnInner(TInner value, TKey key)
        {
            try
            {
                _orchestrator.OnInner(value, key);
            }
            catch (Exception error)
            {
                _emitter.OnError(error);
            }
        }

        private void OnDrainComplete(bool wasReentrant)
        {
            // Counter == 0 means source and every tracked inner have terminated. This is the
            // authoritative source of truth for "this is the last drain"; using a separate latched
            // flag races when the orchestrator calls Track during the isFinal call (counter goes
            // back to 1 but a latched flag would still say true, and we'd fire OnCompleted while
            // a live inner exists).
            var isFinal = Volatile.Read(ref _subscriptionCounter) == 0;

            try
            {
                _orchestrator.OnDrainComplete(isFinal, wasReentrant);
            }
            catch (Exception error)
            {
                _emitter.OnError(error);
                return;
            }

            // Re-check the counter: if the orchestrator added a tracked subscription during its
            // OnDrainComplete (re-establishing liveness), do not complete. CAS-latch ensures
            // exactly one OnCompleted across any number of repeated drains seeing counter == 0.
            if (Volatile.Read(ref _subscriptionCounter) == 0 && Interlocked.CompareExchange(ref _completionEmitted, 1, 0) == 0)
            {
                _emitter.OnCompleted();
            }
        }

        private void DecrementSubscriptionCount()
        {
            var remaining = Interlocked.Decrement(ref _subscriptionCounter);
            Debug.Assert(remaining >= 0, "Subscription counter should never go negative");
        }
    }
}
