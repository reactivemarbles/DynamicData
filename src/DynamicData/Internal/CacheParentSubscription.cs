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
    private readonly SharedDeliveryQueue _queue;
    private readonly KeyedDisposable<TKey> _childSubscriptions = new();
    private readonly SingleAssignmentDisposable _parentSubscription = new();
    private readonly IObserver<TObserver> _observer;
    private int _subscriptionCounter = 1;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheParentSubscription{TParent, TKey, TChild, TObserver}"/> class.
    /// </summary>
    /// <param name="observer">Observer to use for emitting events.</param>
    protected CacheParentSubscription(IObserver<TObserver> observer)
    {
        _observer = observer;
        _queue = new SharedDeliveryQueue(onDrainComplete: () => EmitChanges(_observer));
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
        disposableContainer.Disposable = observable
            .Finally(CheckCompleted)
            .SubscribeSafe(
                onNext: val => ChildOnNext(val, parentKey),
                onError: _observer.OnError,
                onCompleted: () => RemoveChildSubscription(parentKey));
    }

    protected void RemoveChildSubscription(TKey parentKey) => _childSubscriptions.Remove(parentKey);

    protected void CreateParentSubscription(IObservable<IChangeSet<TParent, TKey>> source) =>
        _parentSubscription.Disposable =
            source
                .SynchronizeSafe(_queue)
                .SubscribeSafe(
                    onNext: ParentOnNext,
                    onError: _observer.OnError,
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

    // This must be called by the derived class on anything passed to AddChildSubscription.
    // Manual step so that the derived class has full control on where it is called.
    // Same-thread reentrant delivery ensures child items are delivered inline during
    // parent processing, preserving the original Synchronize(lock) ordering semantics.
    protected IObservable<T> MakeChildObservable<T>(IObservable<T> observable) =>
        observable.SynchronizeSafe(_queue);

    private void CheckCompleted()
    {
        if (Interlocked.Decrement(ref _subscriptionCounter) == 0)
        {
            _observer.OnCompleted();
        }

        Debug.Assert(_subscriptionCounter >= 0, "Should never be negative");
    }
}
