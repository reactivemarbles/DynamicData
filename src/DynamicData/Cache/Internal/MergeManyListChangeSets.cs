// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;
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
            var pendingUpdates = 0;

            // Always increment the counter OUTSIDE of the lock to signal any thread currently holding the lock
            // to not emit the changeset because more changes are incoming.
            IObservable<IChangeSet<TDestination>> CreateChildObservable(TObject obj, TKey key) =>
                selector(obj, key)
                    .Do(_ => Interlocked.Increment(ref pendingUpdates))
                    .Synchronize(locker!);

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TDestination>();

            // Transform to a cache changeset of child lists, synchronize, and publish.
            var shared = source
                .Transform((obj, key) => new ClonedListChangeSet<TDestination>(CreateChildObservable(obj, key), equalityComparer))
                .Do(_ => Interlocked.Increment(ref pendingUpdates))
                .Synchronize(locker)
                .Publish();

            // Merge the child changeset changes together and apply to the tracker
            var subMergeMany = shared
                .MergeMany(clonedList => clonedList.Source.RemoveIndex())
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, Interlocked.Decrement(ref pendingUpdates) == 0 ? observer : null),
                    observer.OnError,
                    observer.OnCompleted);

            // When a source item is removed, all of its sub-items need to be removed
            var subRemove = shared
                .OnItemRemoved(clonedList => changeTracker.RemoveItems(clonedList.List), invokeOnUnsubscribe: false)
                .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.List))
                .SubscribeSafe(
                    _ =>
                    {
                        if (Interlocked.Decrement(ref pendingUpdates) == 0)
                        {
                            changeTracker.EmitChanges(observer);
                        }
                    },
                    observer.OnError);

            return new CompositeDisposable(shared.Connect(), subMergeMany, subRemove);
        });
}
