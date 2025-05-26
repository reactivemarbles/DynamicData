// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer =>
#if false
        {
            var locker = InternalEx.NewLock();
            var cache = new Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey>();
            var parentUpdate = false;

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => cache.Items, comparer, equalityComparer);

            // Transform to a cache changeset of child caches, synchronize, update the local copy, and publish.
            var shared = source
                .Transform((obj, key) => new ChangeSetCache<TDestination, TDestinationKey>(selector(obj, key).Synchronize(locker)))
                .Synchronize(locker)
                .Do(changes =>
                {
                    cache.Clone(changes);
                    parentUpdate = true;
                })
                .Publish();

            // Merge the child changeset changes together and apply to the tracker
            var subMergeMany = shared
                .MergeMany(cacheChangeSet => cacheChangeSet.Source)
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, !parentUpdate ? observer : null),
                    observer.OnError,
                    observer.OnCompleted);

            // When a source item is removed, all of its sub-items need to be removed
            var subRemove = shared
                .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues), invokeOnUnsubscribe: false)
                .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues))
                .SubscribeSafe(
                    _ =>
                    {
                        changeTracker.EmitChanges(observer);
                        parentUpdate = false;
                    },
                    observer.OnError);

            return new CompositeDisposable(shared.Connect(), subMergeMany, subRemove);
        });
#else
        new Subscription(source, selector, observer, equalityComparer, comparer, false));

    // Maintains state for a single subscription
    private sealed class Subscription : IDisposable
    {
#if NET9_0_OR_GREATER
        private readonly Lock _synchronize = new();
#else
        private readonly object _synchronize = new();
#endif
        private readonly Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey> _cache = new();
        private readonly KeyedDisposable<TKey> _childSubscriptions = new();
        private readonly ChangeSetMergeTracker<TDestination, TDestinationKey> _changeSetMergeTracker;
        private readonly Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> _transform;
        private readonly IDisposable _parentSubscription;
        private readonly IObserver<IChangeSet<TDestination, TDestinationKey>> _observer;
        private readonly bool _transformOnRefresh;
        private int _subscriptionCounter = 1;
        private int _updateCounter;

        public Subscription(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> transform,
            IObserver<IChangeSet<TDestination, TDestinationKey>> observer,
            IEqualityComparer<TDestination>? equalityComparer,
            IComparer<TDestination>? comparer,
            bool transformOnRefresh)
        {
            _observer = observer;
            _transform = transform;
            _transformOnRefresh = transformOnRefresh;
            _changeSetMergeTracker = new(() => _cache.Items, comparer, equalityComparer);
            _parentSubscription = source
                .Transform((obj, key) => new ChangeSetCache<TDestination, TDestinationKey>(_transform(obj, key)))
                .Do(_ => IncrementUpdates())
                .Synchronize(_synchronize)
                .SubscribeSafe(ParentOnNext, observer.OnError, CheckCompleted);
        }

        public void Dispose()
        {
            lock (_synchronize)
            {
                _parentSubscription.Dispose();
                _childSubscriptions.Dispose();
            }
        }

        private void ParentOnNext(IChangeSet<ChangeSetCache<TDestination, TDestinationKey>, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the cache and emit the changes
                    case ChangeReason.Add or ChangeReason.Update:
                        _cache.AddOrUpdate(change.Current, change.Key);
                        CreateChildSubscription(change.Current, change.Key);
                        if (change.Previous.HasValue)
                        {
                            _changeSetMergeTracker.RemoveItems(change.Previous.Value.Cache.KeyValues);
                        }
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        _cache.Remove(change.Key);
                        _childSubscriptions.Remove(change.Key);
                        _changeSetMergeTracker.RemoveItems(change.Current.Cache.KeyValues);
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
                _changeSetMergeTracker.EmitChanges(_observer);
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
        private void CreateChildSubscription(ChangeSetCache<TDestination, TDestinationKey> childCache, TKey key)
        {
            // Add a new subscription.  Do first so cleanup of existing subs doesn't trigger OnCompleted.
            Interlocked.Increment(ref _subscriptionCounter);

            // Create a container for the Disposable and add to the KeyedDisposable
            var disposableContainer = _childSubscriptions.Add(key, new SingleAssignmentDisposable());

            // Create the child observable for the source item and update the cache
            // Will Dispose immediately if OnCompleted fires upon subscription because OnCompleted disposes the container
            // Remove the TransformSubscription if it completes because its not needed anymore
            disposableContainer.Disposable = childCache.Source
                .Do(_ => IncrementUpdates())
                .Synchronize(_synchronize)
                .Finally(CheckCompleted)
                .SubscribeSafe(ChildOnNext, _observer.OnError, () => _childSubscriptions.Remove(key));
        }

        private void ChildOnNext(IChangeSet<TDestination, TDestinationKey> changes)
        {
            _changeSetMergeTracker.ProcessChangeSet(changes, null);
            EmitChanges();
        }
    }
#endif
}
