// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
    {
        var cache = new ChangeAwareCache<TDestination, TKey>();
        var locker = new object();
        var parentUpdate = false;

        // Helper to emit any pending changes when appropriate
        void EmitChanges(bool fromParent)
        {
            if (fromParent || !parentUpdate)
            {
                var changes = cache!.CaptureChanges();
                if (changes.Count > 0)
                {
                    observer.OnNext(changes);
                }

                parentUpdate = false;
            }
        }

        // Create the sub-observable that takes the result of the transformation,
        // filters out unchanged values, and then updates the cache
        IObservable<TDestination> CreateSubObservable(TSource obj, TKey key) =>
            transform(obj, key)
                .DistinctUntilChanged()
                .Synchronize(locker!)
                .Do(val => cache!.AddOrUpdate(val, key));

        // Flag a parent update is happening once inside the lock
        var shared = source
            .Synchronize(locker!)
            .Do(_ => parentUpdate = true)
            .Publish();

        // MergeMany automatically handles Add/Update/Remove and OnCompleted/OnError correctly
        var subMerged = shared
            .MergeMany(CreateSubObservable)
            .SubscribeSafe(_ => EmitChanges(fromParent: false), observer.OnError, observer.OnCompleted);

        // Subscribe to the shared Observable to handle Remove events.  MergeMany will unsubscribe from the sub-observable,
        // but the corresponding key value needs to be removed from the Cache so the remove is observed downstream.
        var subRemove = shared
            .OnItemRemoved((_, key) => cache!.Remove(key), invokeOnUnsubscribe: false)
            .SubscribeSafe(_ => EmitChanges(fromParent: true), observer.OnError);

        return new CompositeDisposable(shared.Connect(), subMerged, subRemove);
    });
}
