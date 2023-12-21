// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.List.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles List ChangeSets.
/// </summary>
internal sealed class MergeManyListChangeSets<TObject, TKey, TDestination>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination>>> selector, IEqualityComparer<TDestination>? equalityComparer)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination>> Run() => Observable.Create<IChangeSet<TDestination>>(
            observer =>
            {
                var locker = new object();

                // Transform to a changeset of Cloned Child Lists and Share
                var shared = source
                    .Transform((obj, key) => new ClonedListChangeSet<TDestination>(selector(obj, key).Synchronize(locker), equalityComparer))
                    .Publish();

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination>();

                // Merge all of the children changesets together and apply to the tracker
                var allChanges = shared
                    .MergeMany(clonedList => clonedList.Source.RemoveIndex())
                    .Subscribe(
                        changes => changeTracker.ProcessChangeSet(changes, observer),
                        observer.OnError,
                        observer.OnCompleted);

                // When a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .OnItemRemoved(clonedList => changeTracker.RemoveItems(clonedList.List, observer), invokeOnUnsubscribe: false)
                    .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.List, observer))
                    .Subscribe();

                return new CompositeDisposable(allChanges, removedItems, shared.Connect());
            });
}
