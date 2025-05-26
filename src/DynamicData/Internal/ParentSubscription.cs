// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Base class for subscriptions that need to manage child subscriptions and emit updates
/// when either the parent or child gets a new value.
/// </summary>
/// <typeparam name="TParent">Type of the Parent ChangeSet.</typeparam>
/// <typeparam name="TKey">Type for the Parent ChangeSet Key.</typeparam>
/// <typeparam name="TChild">Type for the Child Subscriptions.</typeparam>
/// <typeparam name="TObserver">Type for the Final Observable.</typeparam>
/// <param name="observer">Observer to use for emitting events.</param>
internal abstract class ParentSubscription<TParent, TKey, TChild, TObserver>(IObserver<TObserver> observer) : IDisposable
    where TParent : notnull
    where TKey : notnull
    where TChild : notnull
{
#if NET9_0_OR_GREATER
    private readonly Lock _synchronize = new();
#else
    private readonly object _synchronize = new();
#endif
    private readonly KeyedDisposable<TKey> _childSubscriptions = new();
    private readonly SingleAssignmentDisposable _parentSubscription = new();
    private readonly IObserver<TObserver> _observer = observer;
    private int _subscriptionCounter = 1;
    private int _updateCounter;
    private bool _disposedValue;

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected abstract void EmitChanges(IObserver<TObserver> observer);

    protected abstract void ParentOnNext(IChangeSet<TParent, TKey> changes);

    protected abstract void ChildOnNext(TChild child, TKey parentKey);

    protected void AddChildSubscription(IObservable<TChild> observable, TKey parentKey)
    {
        // Add a new subscription.  Do first so cleanup of existing subs doesn't trigger OnCompleted.
        Interlocked.Increment(ref _subscriptionCounter);

        // Create a container for the Disposable and add to the KeyedDisposable
        var disposableContainer = _childSubscriptions.Add(parentKey, new SingleAssignmentDisposable());

        // Create the subscription
        // Will Dispose immediately if OnCompleted fires upon subscription because OnCompleted disposes the container
        // Remove the child subscription if it completes because its not needed anymore
        disposableContainer.Disposable = observable
            .Do(_ => EnterUpdate())
            .Synchronize(_synchronize)
            .Finally(CheckCompleted)
            .SubscribeSafe(
                val =>
                {
                    ChildOnNext(val, parentKey);
                    ExitUpdate();
                },
                _observer.OnError,
                () => RemoveChildSubscription(parentKey));
    }

    protected void RemoveChildSubscription(TKey parentKey) => _childSubscriptions.Remove(parentKey);

    protected void CreateParentSubscription(IObservable<IChangeSet<TParent, TKey>> source) =>
        _parentSubscription.Disposable =
            source
                .Do(_ => EnterUpdate())
                .Synchronize(_synchronize)
                .SubscribeSafe(
                    changes =>
                    {
                        ParentOnNext(changes);
                        ExitUpdate();
                    },
                    _observer.OnError,
                    CheckCompleted);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                lock (_synchronize)
                {
                    _parentSubscription.Dispose();
                    _childSubscriptions.Dispose();
                }
            }
            _disposedValue = true;
        }
    }

    private void EnterUpdate() => Interlocked.Increment(ref _updateCounter);

    private void ExitUpdate()
    {
        if (Interlocked.Decrement(ref _updateCounter) == 0)
        {
            EmitChanges(_observer);
        }

        Debug.Assert(_updateCounter >= 0, "Should never be negative");
    }

    private void CheckCompleted()
    {
        if (Interlocked.Decrement(ref _subscriptionCounter) == 0)
        {
            _observer.OnCompleted();
        }

        Debug.Assert(_subscriptionCounter >= 0, "Should never be negative");
    }
}
