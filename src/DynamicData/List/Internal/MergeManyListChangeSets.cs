// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles List ChangeSets.
/// </summary>
internal class MergeManyListChangeSets<TObject, TDestination>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> selector, IEqualityComparer<TDestination>? equalityComparer = null)
    where TObject : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination>> Run() =>
        Observable.Create<IChangeSet<TDestination>>(
            observer =>
            {
                var locker = new object();

                // Transform to an observable list of cached lists
                var sourceListofLists = source
                                            .Transform(obj => new ClonedListChangeSet<TDestination>(selector(obj), equalityComparer, locker))
                                            .AsObservableList();

                var shared = sourceListofLists.Connect().Publish();

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination>();

                // Merge the items back together
                var allChanges = shared.MergeMany(clonedList => clonedList.Source.RemoveIndex())
                                                 .Subscribe(
                                                        changes => changeTracker.ProcessChangeSet(changes, observer),
                                                        observer.OnError,
                                                        observer.OnCompleted);

                // When a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .Synchronize(locker)
                    .OnItemRemoved(mc => changeTracker.RemoveItems(mc.List, observer), invokeOnUnsubscribe: false)
                    .Subscribe();

                return new CompositeDisposable(sourceListofLists, allChanges, removedItems, shared.Connect());
            });
}
