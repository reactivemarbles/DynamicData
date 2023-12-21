// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles List ChangeSets.
/// </summary>
internal sealed class MergeManyListChangeSets<TObject, TDestination>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> selector, IEqualityComparer<TDestination>? equalityComparer)
    where TObject : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination>> Run() =>
        Observable.Create<IChangeSet<TDestination>>(
            observer =>
            {
                var locker = new object();

                // Transform to a changeset of Cloned Child Lists and then Share
                var sourceListOfLists = source
                                            .Transform(obj => new ClonedListChangeSet<TDestination>(selector(obj).Synchronize(locker), equalityComparer))
                                            .AsObservableList();

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination>();

                // Share a connection to the source cache
                var shared = sourceListOfLists.Connect().Publish();

                // Merge the items back together
                var allChanges = shared
                    .Synchronize(locker)
                    .MergeMany(clonedList => clonedList.Source.RemoveIndex())
                    .Subscribe(
                        changes => changeTracker.ProcessChangeSet(changes, observer),
                        observer.OnError,
                        observer.OnCompleted);

                // When a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .Synchronize(locker)
                    .OnItemRemoved(mc => changeTracker.RemoveItems(mc.List, observer), invokeOnUnsubscribe: false)
                    .Subscribe();

                return new CompositeDisposable(sourceListOfLists, allChanges, removedItems, shared.Connect());
            });
}
