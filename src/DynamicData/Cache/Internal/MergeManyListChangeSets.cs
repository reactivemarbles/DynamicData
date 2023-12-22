// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination>();

                // Transform to a cache changeset of child lists
                return source.Transform((obj, key) => new ClonedListChangeSet<TDestination>(selector(obj, key).Synchronize(locker), equalityComparer))

                    // Everything below has to happen inside of the same lock (that is shared with the child collection changes)
                    .Synchronize(locker)

                    // When a source item is removed, all of its sub-items need to be removed
                    .OnItemRemoved(clonedList => changeTracker.RemoveItems(clonedList.List, observer), invokeOnUnsubscribe: false)
                    .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.List, observer))

                    // Merge all the child changesets together and send downstream
                    .MergeMany(clonedList => clonedList.Source.RemoveIndex())
                    .Subscribe(
                        changes => changeTracker.ProcessChangeSet(changes, observer),
                        observer.OnError,
                        observer.OnCompleted);
            });
}
