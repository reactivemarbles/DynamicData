// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class TransformManyAsync<TSource, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> selector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer, Action<Error<TSource, TKey>>? errorHandler = null)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer =>
        {
            var cache = new Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey>();
            var locker = new object();
            var pendingUpdates = 0;

            // Transformation Function:
            // Create the Child Observable by invoking the async selector, appending the counter and the synchronize
            // Pass the result to a new ChangeSetCache instance.
            ChangeSetCache<TDestination, TDestinationKey> Transform_(TSource obj, TKey key) => new(
                Observable.Defer(() => selector(obj, key))
                        .Do(_ => Interlocked.Increment(ref pendingUpdates))
                        .Synchronize(locker!));

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => cache.Items, comparer, equalityComparer);

            // Transform to a cache changeset of child caches, synchronize, clone changes to the local copy, and publish.
            // Always increment the counter OUTSIDE of the lock to signal any thread currently holding the lock
            // to not emit the changeset because more changes are incoming.
            var shared =
                (errorHandler is null ? source.Transform(Transform_) : source.TransformSafe(Transform_, errorHandler))
                    .Do(_ => Interlocked.Increment(ref pendingUpdates))
                    .Synchronize(locker)
                    .Do(cache.Clone)
                    .Publish();

            // Merge the child changeset changes together and apply to the tracker
            // Emit the changeset if there are no other pending changes
            var subMergeMany = shared
                .MergeMany(cacheChangeSet => cacheChangeSet.Source)
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, Interlocked.Decrement(ref pendingUpdates) == 0 ? observer : null),
                    observer.OnError);

            // When a source item is removed, all of its sub-items need to be removed
            // Emit the changeset if there are no other pending changes
            var subRemove = shared
                .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues), invokeOnUnsubscribe: false)
                .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues))
                .SubscribeSafe(
                    _ =>
                    {
                        if (Interlocked.Decrement(ref pendingUpdates) == 0)
                        {
                            changeTracker.EmitChanges(observer);
                        }
                    },
                    observer.OnError,
                    () =>
                    {
                        if (Volatile.Read(ref pendingUpdates) == 0)
                        {
                            observer.OnCompleted();
                        }
                        else
                        {
                            changeTracker.MarkComplete();
                        }
                    });

            return new CompositeDisposable(shared.Connect(), subMergeMany, subRemove);
        });
}
