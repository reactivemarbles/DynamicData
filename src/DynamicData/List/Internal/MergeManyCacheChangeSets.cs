// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Cache.Internal;

namespace DynamicData.List.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    where TObject : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() =>
        Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
            observer =>
            {
                var locker = new object();
                var list = new List<ChangeSetCache<TDestination, TDestinationKey>>();

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => list, comparer, equalityComparer);

                // Transform to a list changeset of child caches, synchronize, update the local copy, and publish.
                var shared = source
                    .Transform(obj => new ChangeSetCache<TDestination, TDestinationKey>(changeSetSelector(obj).Synchronize(locker)))
                    .Synchronize(locker)
                    .Do(list.Clone)
                    .Publish();

                // Merge the child changeset changes together and apply to the tracker
                var subMergeMany = shared
                    .MergeMany(chanceSetCache => chanceSetCache.Source)
                    .Subscribe(
                        changes => changeTracker.ProcessChangeSet(changes, observer),
                        observer.OnError,
                        observer.OnCompleted);

                // When a source item is removed, all of its sub-items need to be removed
                var subRemove = shared
                    .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues, observer), invokeOnUnsubscribe: false)
                    .Subscribe();

                return new CompositeDisposable(shared.Connect(), subMergeMany, subRemove);
            });
}
