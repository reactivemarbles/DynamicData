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

                // Transform to an observable list of merge containers.
                var sourceListOfCaches = source
                                            .Transform(obj => new ChangeSetCache<TDestination, TDestinationKey>(changeSetSelector(obj)))
                                            .Synchronize(locker)
                                            .AsObservableList();

                var shared = sourceListOfCaches.Connect().Publish();

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => sourceListOfCaches.Items.ToArray(), comparer, equalityComparer);

                // When a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .OnItemRemoved(mc => changeTracker.RemoveItems(mc.Cache.KeyValues, observer))
                    .Subscribe();

                // Merge the items back together
                var allChanges = shared.MergeMany(mc => mc.Source)
                                                 .Synchronize(locker)
                                                 .Subscribe(
                                                        changes => changeTracker.ProcessChangeSet(changes, observer),
                                                        observer.OnError,
                                                        observer.OnCompleted);

                return new CompositeDisposable(sourceListOfCaches, allChanges, removedItems, shared.Connect());
            });
}
