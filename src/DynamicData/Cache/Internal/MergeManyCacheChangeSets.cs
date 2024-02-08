// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
        {
            var locker = new object();
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
}
