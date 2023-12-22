// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
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

                // Transform to an observable changeset of cached changesets
                var sourceCacheOfCaches = source
                    .Transform((obj, key) => new ChangeSetCache<TDestination, TDestinationKey>(selector(obj, key).Synchronize(locker)))
                    .AsObservableCache();

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => sourceCacheOfCaches.Items, comparer, equalityComparer);

                // Share a connection to the source cache
                var shared = sourceCacheOfCaches.Connect().Synchronize(locker).Publish();

                // Merge the child changeset changes together and apply to the tracker
                var allChanges = shared
                    .MergeMany(mc => mc.Source)
                    .Subscribe(
                        changes => changeTracker.ProcessChangeSet(changes, observer),
                        observer.OnError,
                        observer.OnCompleted);

                // When a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .OnItemRemoved(mc => changeTracker.RemoveItems(mc.Cache.KeyValues, observer), invokeOnUnsubscribe: false)
                    .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues, observer))
                    .Subscribe();

                return new CompositeDisposable(sourceCacheOfCaches, allChanges, removedItems, shared.Connect());
            });
}
