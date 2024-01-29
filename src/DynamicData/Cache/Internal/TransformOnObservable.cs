// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
    {
        var locker = new object();
        var cache = new ChangeAwareCache<TDestination, TKey>();
        var updateCounter = 0;

        // Helper to emit any pending changes when all the updates have been handled
        void EmitChanges()
        {
            if (Interlocked.Decrement(ref updateCounter) == 0)
            {
                var changes = cache!.CaptureChanges();
                if (changes.Count > 0)
                {
                    observer.OnNext(changes);
                }
            }
        }

        IObservable<TDestination> CreateSubObservable(TSource obj, TKey key) =>
            transform(obj, key)
                .DistinctUntilChanged()
                .Do(_ => Interlocked.Increment(ref updateCounter))
                .Synchronize(locker!)
                .Do(val => cache!.AddOrUpdate(val, key));

        var shared = source
            .Do(_ => Interlocked.Increment(ref updateCounter))
            .Synchronize(locker!)
            .Publish();

        // Use MergeMany because it automatically handles OnCompleted/OnError correctly
        var subMerged = shared
            .MergeMany(CreateSubObservable)
            .SubscribeSafe(_ => EmitChanges(), observer.OnError, observer.OnCompleted);

        // Subscribe to the shared Observable to handle Remove events.  MergeMany will unsubscribe from the sub-observable,
        // but the corresponding key value needs to be removed from the Cache so the remove is observed downstream.
        var subRemove = shared
            .Do(changes => changes.Where(c => c.Reason == ChangeReason.Remove).ForEach(c => cache!.Remove(c.Key)))
            .SubscribeSafe(_ => EmitChanges());

        return new CompositeDisposable(shared.Connect(), subMerged, subRemove);
    });
}
