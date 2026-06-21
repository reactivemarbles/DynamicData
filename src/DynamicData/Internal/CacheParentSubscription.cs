// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Internal;
#else

namespace DynamicData.Internal;
#endif

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
    /// <summary>
    /// The _childSubscriptions field.
    /// </summary>
    private readonly KeyedDisposable<TKey> _childSubscriptions = new();

    /// <summary>
    /// The _parentSubscription field.
    /// </summary>
    private readonly SingleAssignmentDisposable _parentSubscription = new();

    /// <summary>
    /// The _queue field.
    /// </summary>
    private readonly SharedDeliveryQueue _queue;

    /// <summary>
    /// The _observer field.
    /// </summary>
    private readonly IObserver<TObserver> _observer;

    /// <summary>
    /// The _subscriptionCounter field.
    /// </summary>
    private int _subscriptionCounter = 1; // Starts at 1 for the parent subscription

    /// <summary>
    /// The _isCompleted field.
    /// </summary>
    private bool _isCompleted;

    /// <summary>
    /// The _hasTerminated field.
    /// </summary>
    private bool _hasTerminated;

    /// <summary>
    /// The _disposedValue field.
    /// </summary>
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

    /// <summary>
    /// Executes the ParentOnNext operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    protected abstract void ParentOnNext(IChangeSet<TParent, TKey> changes);

    /// <summary>
    /// Executes the ChildOnNext operation.
    /// </summary>
    /// <param name="child">The child value.</param>
    /// <param name="parentKey">The parentKey value.</param>
    protected abstract void ChildOnNext(TChild child, TKey parentKey);

    /// <summary>
    /// Executes the EmitChanges operation.
    /// </summary>
    /// <param name="observer">The observer value.</param>
    protected abstract void EmitChanges(IObserver<TObserver> observer);

    /// <summary>
    /// Executes the AddChildSubscription operation.
    /// </summary>
    /// <param name="observable">The observable value.</param>
    /// <param name="parentKey">The parentKey value.</param>
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

    /// <summary>
    /// Executes the RemoveChildSubscription operation.
    /// </summary>
    /// <param name="parentKey">The parentKey value.</param>
    protected void RemoveChildSubscription(TKey parentKey) => _childSubscriptions.Remove(parentKey);

    /// <summary>
    /// Executes the CreateParentSubscription operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    protected void CreateParentSubscription(IObservable<IChangeSet<TParent, TKey>> source) =>
        _parentSubscription.Disposable =
            source
                .SynchronizeSafe(_queue)
                .SubscribeSafe(
                    onNext: ParentOnNext,
                    onError: TerminalError,
                    onCompleted: CheckCompleted);

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    /// <param name="disposing">The disposing value.</param>
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
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="observable">The observable value.</param>
    /// <returns>The result of the operation.</returns>
    protected IObservable<T> MakeChildObservable<T>(IObservable<T> observable) =>
        observable.SynchronizeSafe(_queue);

    /// <summary>
    /// Executes the OnDrainComplete operation.
    /// </summary>
    private void OnDrainComplete()
    {
        EmitChanges(_observer);

        if (Volatile.Read(ref _isCompleted) && !_hasTerminated)
        {
            _hasTerminated = true;
            _observer.OnCompleted();
        }
    }

    /// <summary>
    /// Executes the TerminalError operation.
    /// </summary>
    /// <param name="error">The error value.</param>
    private void TerminalError(Exception error)
    {
        _hasTerminated = true;
        _observer.OnError(error);
    }

    /// <summary>
    /// Executes the CheckCompleted operation.
    /// </summary>
    private void CheckCompleted()
    {
        if (Interlocked.Decrement(ref _subscriptionCounter) == 0)
        {
            Volatile.Write(ref _isCompleted, true);
        }

        Debug.Assert(_subscriptionCounter >= 0, "Should never be negative");
    }
}
