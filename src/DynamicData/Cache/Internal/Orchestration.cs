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
/// state owned by the <see cref="OrchestratorContext"/> is recreated on every subscribe.
/// </summary>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream.</typeparam>
internal sealed class Orchestration<TSource, TKey, TInner, TResult>(IObservable<IChangeSet<TSource, TKey>> source, ICacheOrchestrator<TSource, TKey, TInner, TResult> orchestrator)
    where TSource : notnull
    where TKey : notnull
    where TInner : notnull
{
    public IObservable<TResult> Run() => Observable.Create<TResult>(observer => new OrchestratorContext(source, observer, orchestrator));

    private sealed class OrchestratorContext : ICacheOrchestratorContext<TKey, TInner>, IDisposable
    {
        private readonly KeyedDisposable<TKey> _innerSubscriptions = new();
        private readonly SingleAssignmentDisposable _sourceSubscription = new();
        private readonly SharedDeliveryQueue _queue;
        private readonly DeliverySubQueue<TResult> _emitter;
        private readonly ICacheOrchestrator<TSource, TKey, TInner, TResult> _orchestrator;
        private int _subscriptionCounter = 1;
        private bool _isCompleted;
        private bool _disposed;

        public OrchestratorContext(IObservable<IChangeSet<TSource, TKey>> source, IObserver<TResult> observer, ICacheOrchestrator<TSource, TKey, TInner, TResult> orchestrator)
        {
            _orchestrator = orchestrator;
            _queue = new SharedDeliveryQueue(onDrainComplete: OnDrainComplete);

            // Create the emitter sub-queue first (lowest index, drains last LIFO) so source-triggered
            // sync inner emissions (higher index) deliver first and any orchestrator emit lands on the
            // emitter after that work has settled.
            _emitter = _queue.CreateQueue(observer);
            _orchestrator.Initialize(this, _emitter);
            SubscribeToSource(source);
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
            _emitter.Dispose();
        }

        public void Track(TKey key, IObservable<TInner>? observable)
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
                    onNext: value => OnInner(value, key),
                    onError: _emitter.OnError,
                    onCompleted: () => _innerSubscriptions.Remove(key));
        }

        public IObservable<T> Serialize<T>(IObservable<T> observable) => observable.SynchronizeSafe(_queue);

        private void SubscribeToSource(IObservable<IChangeSet<TSource, TKey>> source) =>
            _sourceSubscription.Disposable = source
                .SynchronizeSafe(_queue)
                .SubscribeSafe(
                    onNext: OnSourceChangeSet,
                    onError: _emitter.OnError,
                    onCompleted: DecrementSubscriptionCount);

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

        private void OnDrainComplete()
        {
            try
            {
                _orchestrator.OnDrainComplete();
            }
            catch (Exception error)
            {
                _emitter.OnError(error);
                return;
            }

            if (Volatile.Read(ref _isCompleted))
            {
                // Latch off so the inevitable reentrant drain (triggered by enqueueing OnCompleted on
                // the emitter sub-queue) doesn't try to complete the stream twice.
                Volatile.Write(ref _isCompleted, false);
                _emitter.OnCompleted();
            }
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
