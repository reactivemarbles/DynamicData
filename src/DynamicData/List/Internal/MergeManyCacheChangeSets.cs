// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Internal;

namespace DynamicData.List.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    where TObject : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer =>
        {
            var list = new List<ChangeSetCache<TDestination, TDestinationKey>>();
            var locker = new object();
            var pendingUpdates = 0;

            // Always increment the counter OUTSIDE of the lock to signal any thread currently holding the lock
            // to not emit the changeset because more changes are incoming.
            IObservable<IChangeSet<TDestination, TDestinationKey>> CreateChildObservable(TObject obj) =>
                changeSetSelector(obj)
                    .Do(_ => Interlocked.Increment(ref pendingUpdates))
                    .Synchronize(locker!);

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => list, comparer, equalityComparer);

            // Transform to a list changeset of child caches, synchronize, update the local copy, and publish.
            var shared = source
                .Do(_ => Interlocked.Increment(ref pendingUpdates))
                .Synchronize(locker)
                .Transform(obj => new ChangeSetCache<TDestination, TDestinationKey>(CreateChildObservable(obj)))
                .Do(list.Clone)
                .Publish();

            // Merge the child changeset changes together and apply to the tracker
            var subMergeMany = shared
                .MergeMany(chanceSetCache => chanceSetCache.Source)
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, Interlocked.Decrement(ref pendingUpdates) == 0 ? observer : null),
                    observer.OnError,
                    () =>
                    {
                        if (Volatile.Read(ref pendingUpdates) == 0)
                        {
                            observer.OnCompleted();
                        }
                        else
                        {
                            changeTracker.MarkComplete();
                        }
                    });

            // When a source item is removed, all of its sub-items need to be removed
            var subRemove = shared
                .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues), invokeOnUnsubscribe: false)
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
