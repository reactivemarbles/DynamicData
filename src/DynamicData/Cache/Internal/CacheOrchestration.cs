// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// One subscription to an orchestrated operator. Owns the source subscription, the per-key inner subscriptions and
/// the <see cref="SharedDeliveryQueue"/> that serializes them, and drives the
/// <see cref="CacheOrchestrator{TSource, TKey, TInner, TResult}"/> built for it.
/// </summary>
/// <remarks>
/// <para>
/// Every notification from the source and from the tracked inner observables goes through the queue, which never
/// holds its lock while a hook runs. A cross-cache pipeline can therefore not deadlock on this operator.
/// </para>
/// <para>
/// Each delivery opens a frame. Deliveries the queue runs inline beneath it, on the same thread, open and close
/// frames of their own, and only the outermost one calls
/// <see cref="CacheOrchestrator{TSource, TKey, TInner, TResult}.OnFrameEnded"/>, so one upstream notification and
/// everything it triggers synchronously produce a single emission. No lock guards the depth because the queue has
/// already serialized delivery.
/// </para>
/// <para>
/// The downstream observer completes at the end of the first frame in which the source and every tracked inner
/// observable have completed. The first error from any of them, or thrown by a hook, terminates it.
/// </para>
/// </remarks>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream.</typeparam>
internal sealed class CacheOrchestration<TSource, TKey, TInner, TResult> : IDisposable
    where TSource : notnull
    where TKey : notnull
    where TInner : notnull
{
    private readonly KeyedDisposable<TKey> _innerSubscriptions = new();
    private readonly SingleAssignmentDisposable _sourceSubscription = new();
    private readonly SharedDeliveryQueue _queue = new();
    private readonly IObserver<TResult> _observer;
    private readonly CacheOrchestrator<TSource, TKey, TInner, TResult> _orchestrator;
    private int _subscriptionCounter = 1; // Starts at 1 for the source subscription
    private int _frameDepth;
    private bool _hasTerminated;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheOrchestration{TSource, TKey, TInner, TResult}"/> class and
    /// subscribes to <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="observer">The downstream observer.</param>
    /// <param name="factory">Builds the orchestrator for this subscription from the orchestration and the downstream observer.</param>
    public CacheOrchestration(IObservable<IChangeSet<TSource, TKey>> source, IObserver<TResult> observer, Func<CacheOrchestration<TSource, TKey, TInner, TResult>, IObserver<TResult>, CacheOrchestrator<TSource, TKey, TInner, TResult>> factory)
    {
        _observer = observer;
        _orchestrator = factory(this, observer);
        _sourceSubscription.Disposable = source
            .SynchronizeSafe(_queue)
            .SubscribeSafe(OnSourceNext, OnError, OnSourceCompleted);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Stop incoming work before terminating the queue, so nothing is fed into a queue that is shutting down.
        _sourceSubscription.Dispose();
        _innerSubscriptions.Dispose();
        _queue.Dispose();
        (_orchestrator as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Subscribes to <paramref name="observable"/> as the inner observable for <paramref name="key"/>, replacing and
    /// disposing any inner observable already tracked for it. Its values reach
    /// <see cref="CacheOrchestrator{TSource, TKey, TInner, TResult}.OnItemSourceNext"/> through the queue, and the
    /// downstream observer does not complete while it is still active.
    /// </summary>
    /// <param name="key">The source key to track the inner observable against.</param>
    /// <param name="observable">The inner observable.</param>
    public void Track(TKey key, IObservable<TInner> observable)
    {
        // Count the new subscription before installing it, so disposing the one it replaces cannot take the
        // counter to zero in between.
        Interlocked.Increment(ref _subscriptionCounter);

        var container = _innerSubscriptions.Add(key, new SingleAssignmentDisposable());

        // Finally fires on completion, error and disposal, so the counter always comes back down. onCompleted only
        // fires on completion, so an inner observable that is replaced or untracked does not remove its successor.
        container.Disposable = observable
            .SynchronizeSafe(_queue)
            .Finally(DecrementSubscriptionCount)
            .SubscribeSafe(value => OnItemSourceNext(value, key), OnError, () => OnItemSourceCompleted(key));
    }

    /// <summary>Disposes the inner observable tracked for <paramref name="key"/>, if there is one.</summary>
    /// <param name="key">The source key whose inner observable to dispose.</param>
    public void Untrack(TKey key) => _innerSubscriptions.Remove(key);

    /// <summary>
    /// Routes <paramref name="observable"/> through the same queue as the source and the inner observables, so its
    /// subscriber never runs at the same time as a hook. Unlike <see cref="Track"/>, it does not hold off downstream
    /// completion.
    /// </summary>
    /// <typeparam name="T">Type of values emitted by <paramref name="observable"/>.</typeparam>
    /// <param name="observable">The observable to serialize.</param>
    /// <returns>An observable that delivers through the orchestration's queue.</returns>
    public IObservable<T> Serialize<T>(IObservable<T> observable) => observable.SynchronizeSafe(_queue);

    private void OnSourceNext(IChangeSet<TSource, TKey> changes)
    {
        ++_frameDepth;

        try
        {
            _orchestrator.OnSourceNext(changes);
        }
        catch (Exception error)
        {
            OnError(error);
        }

        EndFrame();
    }

    private void OnItemSourceNext(TInner value, TKey key)
    {
        ++_frameDepth;

        try
        {
            _orchestrator.OnItemSourceNext(value, key);
        }
        catch (Exception error)
        {
            OnError(error);
        }

        EndFrame();
    }

    private void OnSourceCompleted()
    {
        ++_frameDepth;
        DecrementSubscriptionCount();
        EndFrame();
    }

    private void OnItemSourceCompleted(TKey key)
    {
        ++_frameDepth;
        _innerSubscriptions.Remove(key);
        EndFrame();
    }

    private void EndFrame()
    {
        if (--_frameDepth != 0 || _hasTerminated)
        {
            return;
        }

        try
        {
            _orchestrator.OnFrameEnded(Volatile.Read(ref _subscriptionCounter) == 0);
        }
        catch (Exception error)
        {
            OnError(error);
            return;
        }

        // Read the counter again: OnFrameEnded can track a new inner observable, which keeps the stream alive.
        if (Volatile.Read(ref _subscriptionCounter) == 0 && !_hasTerminated)
        {
            _hasTerminated = true;
            _observer.OnCompleted();
        }
    }

    private void OnError(Exception error)
    {
        if (_hasTerminated)
        {
            return;
        }

        _hasTerminated = true;
        _observer.OnError(error);
    }

    private void DecrementSubscriptionCount()
    {
        var remaining = Interlocked.Decrement(ref _subscriptionCounter);
        Debug.Assert(remaining >= 0, "Subscription counter should never go negative");
    }
}
