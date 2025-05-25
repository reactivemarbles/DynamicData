// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, bool transformOnRefresh = false)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() =>
        Observable.Create<IChangeSet<TDestination, TKey>>(observer => new Subscription(source, transform, observer, transformOnRefresh));

    // Maintains state for a single subscription
    private sealed class Subscription : IDisposable
    {
#if NET9_0_OR_GREATER
        private readonly Lock _synchronize = new();
#else
        private readonly object _synchronize = new();
#endif
        private readonly ChangeAwareCache<TDestination, TKey> _cache = new();
        private readonly KeyedDisposable<TKey> _transformSubscriptions = new();
        private readonly Func<TSource, TKey, IObservable<TDestination>> _transform;
        private readonly IDisposable _sourceSubscription;
        private readonly IObserver<IChangeSet<TDestination, TKey>> _observer;
        private readonly bool _transformOnRefresh;
        private int _subscriptionCounter = 1;
        private int _updateCounter;

        public Subscription(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, IObserver<IChangeSet<TDestination, TKey>> observer, bool transformOnRefresh)
        {
            _observer = observer;
            _transform = transform;
            _transformOnRefresh = transformOnRefresh;
            _sourceSubscription = source
                .Do(_ => IncrementUpdates())
                .Synchronize(_synchronize)
                .SubscribeSafe(ProcessSourceChangeSet, observer.OnError, CheckCompleted);
        }

        public void Dispose()
        {
            lock (_synchronize)
            {
                _sourceSubscription.Dispose();
                _transformSubscriptions.Dispose();
            }
        }

        private void ProcessSourceChangeSet(IChangeSet<TSource, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the cache and emit the changes
                    case ChangeReason.Add or ChangeReason.Update:
                        CreateTransformSubscription(change.Current, change.Key);
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        _transformSubscriptions.Remove(change.Key);
                        _cache.Remove(change.Key);
                        break;

                    case ChangeReason.Refresh:
                        if (_transformOnRefresh)
                        {
                            CreateTransformSubscription(change.Current, change.Key);
                        }
                        else
                        {
                            // Let the downstream decide what this means
                            _cache.Refresh(change.Key);
                        }
                        break;
                }
            }

            // Emit any pending changes
            EmitChanges();
        }

        private void IncrementUpdates() => Interlocked.Increment(ref _updateCounter);

        private void EmitChanges()
        {
            if (Interlocked.Decrement(ref _updateCounter) == 0)
            {
                var changes = _cache.CaptureChanges();
                if (changes.Count > 0)
                {
                    _observer.OnNext(changes);
                }
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

        // Create the sub-observable that takes the result of the transformation,
        // filters out unchanged values, and then updates the cache
        private void CreateTransformSubscription(TSource obj, TKey key)
        {
            // Add a new subscription.  Do first so cleanup of existing subs doesn't trigger OnCompleted.
            Interlocked.Increment(ref _subscriptionCounter);

            // Create a container for the Disposable and add to the KeyedDisposable
            var disposableContainer = _transformSubscriptions.Add(key, new SingleAssignmentDisposable());

            // Create the transformation observable for the source item, filter unchanged, and update the cache
            // Will Dispose immediately if OnCompleted fires upon subscription because OnCompleted disposes the container
            // Remove the TransformSubscription if it completes because its not needed anymore
            disposableContainer.Disposable = _transform(obj, key)
                .DistinctUntilChanged()
                .Do(_ => IncrementUpdates())
                .Synchronize(_synchronize)
                .Finally(CheckCompleted)
                .SubscribeSafe(val => TransformOnNext(val, key), _observer.OnError, () => _transformSubscriptions.Remove(key));
        }

        private void TransformOnNext(TDestination latestValue, TKey key)
        {
            _cache.AddOrUpdate(latestValue, key);
            EmitChanges();
        }
    }
}
