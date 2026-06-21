// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="changeSetSelector">The changeSetSelector value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
/// <param name="comparer">The comparer value.</param>
internal sealed class MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    where TObject : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer =>
        {
            var locker = InternalEx.NewLock();
            var list = new List<ChangeSetCache<TDestination, TDestinationKey>>();
            var parentUpdate = false;

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => list, comparer, equalityComparer);

            // Transform to a list changeset of child caches, synchronize, update the local copy, and publish.
            var shared = source
                .Transform(obj => new ChangeSetCache<TDestination, TDestinationKey>(changeSetSelector(obj).Synchronize(locker)))
                .Synchronize(locker)
                .Do(list.Clone)
                .Do(_ => parentUpdate = true)
                .Publish();

            // Merge the child changeset changes together and apply to the tracker
            var subMergeMany = shared
                .MergeMany(chanceSetCache => chanceSetCache.Source)
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, !parentUpdate ? observer : null),
                    observer.OnError,
                    observer.OnCompleted);

            // When a source item is removed, all of its sub-items need to be removed
            var subRemove = shared
                .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues), invokeOnUnsubscribe: false)
                .Do(_ =>
                {
                    changeTracker.EmitChanges(observer);
                    parentUpdate = false;
                })
                .Subscribe(static _ => { }, observer.OnError);

            return new CompositeDisposable(shared.Connect(), subMergeMany, subRemove);
        });
}
