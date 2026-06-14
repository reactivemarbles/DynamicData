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
/// changeset. A fresh per-subscription <see cref="OrchestratorContext"/> is constructed by
/// <see cref="Run"/> on each subscribe, with the orchestrator built by the supplied
/// <paramref name="factory"/>, so all per-subscription state is naturally isolated.
/// </summary>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream.</typeparam>
/// <typeparam name="TOrch">Concrete orchestrator type returned by the factory. Generic-typed so dispatch sites devirtualize.</typeparam>
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

            try
            {
                // Emitter sub-queue is allocated first so it drains last (LIFO); source-triggered
                // sync inner emissions land on later-indexed queues and deliver before it.
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

            // Stop incoming work before disposing the queue so source/inner pumps can't fire into
            // a terminating queue.
            _sourceSubscription.Dispose();
            _innerSubscriptions.Dispose();
            _queue.Dispose();
            _emitter.Dispose();
            (_orchestrator as IDisposable)?.Dispose();
        }

        public void Track(TKey key, IObservable<TInner> observable)
        {
            Debug.Assert(observable is not null, "Use Untrack(key) to remove a tracked subscription");

            // Increment before installing the new subscription so disposing the prior one (which
            // decrements via Finally) cannot race the counter to zero between Track calls.
            Interlocked.Increment(ref _subscriptionCounter);

            var container = _innerSubscriptions.Add(key, new SingleAssignmentDisposable());

            // Finally fires on completion, error, AND disposal, so the counter always decrements.
            // onCompleted only fires on normal completion, so a subscription disposed by Track
            // replacing it (or by Dispose) does not trigger Remove from inside.
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
            // Counter == 0 means source and every tracked inner have terminated. A latched flag
            // would race when the orchestrator calls Track during the isFinal call.
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

            // Re-check: if OnDrainComplete added a tracked subscription (re-establishing liveness),
            // don't complete. CAS ensures exactly one OnCompleted across repeated drains seeing 0.
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
