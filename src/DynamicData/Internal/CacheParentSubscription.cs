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
/// OnDrainComplete calls EmitChanges after the outermost delivery, outside the lock.
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
        _queue = new SharedDeliveryQueue(onDrainComplete: OnDrainComplete);
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
                onNext: val => ChildOnNext(val, parentKey),
                onError: TerminalError,
                onCompleted: () => RemoveChildSubscription(parentKey));
    }

    protected void RemoveChildSubscription(TKey parentKey) => _childSubscriptions.Remove(parentKey);

    protected void CreateParentSubscription(IObservable<IChangeSet<TParent, TKey>> source) =>
        _parentSubscription.Disposable =
            source
                .SynchronizeSafe(_queue)
                .SubscribeSafe(
                    onNext: ParentOnNext,
                    onError: TerminalError,
                    onCompleted: CheckCompleted);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _queue.EnsureDeliveryComplete();
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

    private void OnDrainComplete()
    {
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
}
