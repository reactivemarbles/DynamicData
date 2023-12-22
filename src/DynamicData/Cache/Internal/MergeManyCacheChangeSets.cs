// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

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

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => cache.Items, comparer, equalityComparer);

            // Transform to a cache changeset of child caches
            return source.Transform((obj, key) => new ChangeSetCache<TDestination, TDestinationKey>(selector(obj, key).Synchronize(locker)))

                // Everything below has to happen inside of the same lock (that is shared with the child collection changes)
                .Synchronize(locker)

                // Update the local collection of parent items
                .Do(cache.Clone)

                // When a source item is removed, all of its sub-items need to be removed
                .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues, observer), invokeOnUnsubscribe: false)
                .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues, observer))

                // Merge the child changeset changes together and apply to the tracker
                .MergeMany(mc => mc.Source)
                .Subscribe(
                    changes => changeTracker.ProcessChangeSet(changes, observer),
                    observer.OnError,
                    observer.OnCompleted);
        });
}
