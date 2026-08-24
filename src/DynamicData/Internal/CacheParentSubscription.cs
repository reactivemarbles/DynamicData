// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Base class for subscriptions that need to manage child subscriptions and emit updates
/// when either the parent or child gets a new value.
/// Uses a <see cref="SharedDeliveryQueue"/> for serialization and lock-free delivery.
/// Same-thread reentrant delivery preserves child-during-parent ordering.
/// Accumulated changes are emitted once per delivery frame, where a frame is one
/// notification plus anything delivered synchronously beneath it on the same thread.
/// </summary>
/// <typeparam name="TParent">Type of the Parent ChangeSet.</typeparam>
/// <typeparam name="TKey">Type for the Parent ChangeSet Key.</typeparam>
/// <typeparam name="TChild">Type for the Child Subscriptions.</typeparam>
/// <typeparam name="TObserver">Type for the Final Observable.</typeparam>
internal abstract class CacheParentSubscription<TParent, TKey, TChild, TObserver> : IDisposable
    where TParent : notnull
    where TKey : notnull
    where TChild : notnull
{
    private readonly KeyedDisposable<TKey> _childSubscriptions = new();
    private readonly SingleAssignmentDisposable _parentSubscription = new();
    private readonly SharedDeliveryQueue _queue;
    private readonly IObserver<TObserver> _observer;
    private int _subscriptionCounter = 1; // Starts at 1 for the parent subscription
    private int _frameDepth;
    private bool _isCompleted;
    private bool _hasTerminated;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheParentSubscription{TParent, TKey, TChild, TObserver}"/> class.
    /// </summary>
    /// <param name="observer">Observer to use for emitting events.</param>
    protected CacheParentSubscription(IObserver<TObserver> observer)
    {
        _observer = observer;
        _queue = new SharedDeliveryQueue();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected abstract void ParentOnNext(IChangeSet<TParent, TKey> changes);

    protected abstract void ChildOnNext(TChild child, TKey parentKey);

    protected abstract void EmitChanges(IObserver<TObserver> observer);

    protected void AddChildSubscription(IObservable<TChild> observable, TKey parentKey)
    {
        // Add a new subscription. Do first so cleanup of existing subs doesn't trigger OnCompleted.
        Interlocked.Increment(ref _subscriptionCounter);

        // Create a container for the Disposable and add to the KeyedDisposable
        var disposableContainer = _childSubscriptions.Add(parentKey, new SingleAssignmentDisposable());

        // Create the subscription
        // Will Dispose immediately if OnCompleted fires upon subscription because OnCompleted disposes the container
        // Remove the child subscription if it completes because its not needed anymore
        //
        // THREADING INVARIANT: Finally(CheckCompleted) fires on completion, error, AND disposal,
        // ensuring the subscription counter always decrements. The onCompleted callback only fires
        // on normal completion (not disposal), so RemoveChildSubscription is NOT called when the
        // parent disposes child subscriptions during Dispose(). This asymmetry is intentional:
        // disposal cleanup is handled by KeyedDisposable, not by individual completion callbacks.
        disposableContainer.Disposable = observable
            .Finally(CheckCompleted)
            .SubscribeSafe(
                onNext: val => DeliverChild(val, parentKey),
                onError: TerminalError,
                onCompleted: () => CompleteChild(parentKey));
    }

    protected void RemoveChildSubscription(TKey parentKey) => _childSubscriptions.Remove(parentKey);

    protected void CreateParentSubscription(IObservable<IChangeSet<TParent, TKey>> source) =>
        _parentSubscription.Disposable =
            source
                .SynchronizeSafe(_queue)
                .SubscribeSafe(
                    onNext: DeliverParent,
                    onError: TerminalError,
                    onCompleted: CompleteParent);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _queue.Dispose();
                _parentSubscription.Dispose();
                _childSubscriptions.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Wraps a child observable through the shared delivery queue for serialization.
    /// Must be called by derived classes on observables passed to <see cref="AddChildSubscription"/>.
    /// Same-thread reentrant delivery ensures child items are delivered inline during
    /// parent processing, preserving the original Synchronize(lock) ordering semantics.
    /// </summary>
    protected IObservable<T> MakeChildObservable<T>(IObservable<T> observable) =>
        observable.SynchronizeSafe(_queue);

    private void DeliverParent(IChangeSet<TParent, TKey> changes)
    {
        using var frame = BeginFrame();
        ParentOnNext(changes);
    }

    private void DeliverChild(TChild child, TKey parentKey)
    {
        using var frame = BeginFrame();
        ChildOnNext(child, parentKey);
    }

    private void CompleteParent()
    {
        using var frame = BeginFrame();
        CheckCompleted();
    }

    private void CompleteChild(TKey parentKey)
    {
        using var frame = BeginFrame();
        RemoveChildSubscription(parentKey);
    }

    /// <summary>
    /// Opens a delivery frame that stays open until the returned <see cref="FrameTracker"/> is disposed.
    /// Deliveries nested beneath this one, which the queue runs inline on the same thread, open and close
    /// their own frame and leave the emit to the outermost, so one upstream notification and everything it
    /// triggers synchronously produce a single downstream changeset. No lock is needed around the depth
    /// because the queue has already serialized delivery.
    /// </summary>
    /// <returns>A tracker that closes the frame when disposed.</returns>
    private FrameTracker BeginFrame()
    {
        ++_frameDepth;
        return new FrameTracker(this);
    }

    /// <summary>
    /// Closes the current delivery frame, emitting the accumulated changes only when the outermost
    /// frame closes.
    /// </summary>
    private void EndFrame()
    {
        if (--_frameDepth != 0)
        {
            return;
        }

        EmitChanges(_observer);

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

    private void CheckCompleted()
    {
        if (Interlocked.Decrement(ref _subscriptionCounter) == 0)
        {
            Volatile.Write(ref _isCompleted, true);
        }

        Debug.Assert(_subscriptionCounter >= 0, "Should never be negative");
    }

    /// <summary>
    /// Closes the delivery frame opened by <see cref="BeginFrame"/> when disposed, so a frame can be
    /// scoped with <see langword="using"/> instead of pairing the calls by hand.
    /// </summary>
    /// <param name="owner">The subscription whose frame is being tracked.</param>
    private readonly struct FrameTracker(CacheParentSubscription<TParent, TKey, TChild, TObserver> owner) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => owner.EndFrame();
    }
}
