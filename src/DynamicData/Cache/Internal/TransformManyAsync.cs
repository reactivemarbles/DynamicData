// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class TransformManyAsync<TSource, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> selector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer, Action<Error<TSource, TKey>>? errorHandler = null)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer =>
        {
            var locker = new object();
            var cache = new Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey>();
            var parentUpdate = false;

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => cache.Items, comparer, equalityComparer);

            // Transform Helper
            async Task<IObservable<IChangeSet<TDestination, TDestinationKey>>> InvokeSelector(TSource obj, TKey key)
            {
                if (errorHandler != null)
                {
                    try
                    {
                        return await selector(obj, key).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        errorHandler.Invoke(new Error<TSource, TKey>(e, obj, key));
                        return Observable.Empty<IChangeSet<TDestination, TDestinationKey>>();
                    }
                }

                return await selector(obj, key).ConfigureAwait(false);
            }

            // Transformation Function:
            // Create the Child Observable by invoking the async selector, appending the synchronize, and creating a new ChangeSetCache instance.
            ChangeSetCache<TDestination, TDestinationKey> Transform_(TSource obj, TKey key) =>
                new(Observable.Defer(() => InvokeSelector(obj, key)).Synchronize(locker!));

            // Transform to a cache changeset of child caches, synchronize, clone changes to the local copy, and publish.
            var shared = source
                .Transform(Transform_)
                .Synchronize(locker)
                .Do(
                    changes =>
                    {
                        cache.Clone(changes);
                        parentUpdate = true;
                    })
                .Publish();

            // Merge the child changeset changes together and apply to the tracker
            // Emit the changeset if not currently handling a parent stream update
            var subMergeMany = shared
                .MergeMany(cacheChangeSet => cacheChangeSet.Source)
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, !parentUpdate ? observer : null),
                    observer.OnError);

            // When a source item is removed, all of its sub-items need to be removed
            // Emit any pending changes
            var subRemove = shared
                .OnItemRemoved(changeSetCache => changeTracker.RemoveItems(changeSetCache.Cache.KeyValues), invokeOnUnsubscribe: false)
                .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues))
                .SubscribeSafe(
                    _ =>
                    {
                        changeTracker.EmitChanges(observer);
                        parentUpdate = false;
                    },
                    observer.OnError,
                    observer.OnCompleted);

            return new CompositeDisposable(shared.Connect(), subMergeMany, subRemove);
        });
}
