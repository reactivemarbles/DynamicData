// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal class MergeManyListChangeSets<TObject, TDestination>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> selector)
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
                                            .Transform(obj => new ChangeSetCache<TDestination>(selector(obj)))
                                            .Synchronize(locker)
                                            .AsObservableList();

                var shared = sourceListofLists.Connect().Publish();

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination>();

                // Berge the items back together
                var allChanges = shared.MergeMany(mc => mc.Source.RemoveIndex())
                                                 .Synchronize(locker)
                                                 .Subscribe(
                                                        changes => changeTracker.ProcessChangeSet(changes, observer),
                                                        observer.OnError,
                                                        observer.OnCompleted);

                // When a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .OnItemRemoved(mc => changeTracker.RemoveItems(mc.List, observer))
                    .Subscribe();

                return new CompositeDisposable(sourceListofLists, allChanges, removedItems, shared.Connect());
            });
}
