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
            var cache = new Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey>();
            var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => cache.Items, comparer, equalityComparer);
            var locker = new object();

            // Always increment the counter OUTSIDE of the lock to signal any thread currently holding the lock
            // to not emit the changeset because more changes are incoming.
            IObservable<IChangeSet<TDestination, TDestinationKey>> CreateChildObservable(TObject obj, TKey key) =>
                selector(obj, key)
                    .Do(_ => changeTracker.TrackIncoming())
                    .Synchronize(locker)
                    ;

            // Transform to a cache changeset of child caches, synchronize, update the local copy, and publish.
            var shared = source
                .Do(_ => changeTracker.TrackIncoming())
                .Transform((obj, key) => new ChangeSetCache<TDestination, TDestinationKey>(source: CreateChildObservable(obj, key)))
                .Publish();

            // Merge the child changeset changes together and apply to the tracker
            var subUpdateCache = shared
                .Synchronize(locker)
                .Do(cache.Clone)
                .Subscribe();

            // Merge the child changeset changes together and apply to the tracker
            var subMergeMany = shared
                .MergeMany(cacheChangeSet => cacheChangeSet.Source)
                .Synchronize(locker)
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, observer),
                    observer.OnError,
                    () => changeTracker.FinalComplete(observer));

            // When a source item is removed, all of its sub-items need to be removed
            var subRemove = shared
                .Synchronize(locker)
                .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues), invokeOnUnsubscribe: false)
                .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues))
                .SubscribeSafe(
                    _ => changeTracker.EmitChanges(observer),
                    observer.OnError);

            return new CompositeDisposable(shared.Connect(), subUpdateCache, subMergeMany, subRemove);
        });
}
