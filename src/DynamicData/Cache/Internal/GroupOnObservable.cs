// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOnObservable<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> selectGroup)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(observer =>
    {
        var grouper = new DynamicGrouper<TObject, TKey, TGroupKey>();
        var locker = new object();
        var parentUpdate = false;

        IObservable<TGroupKey> CreateGroupObservable(TObject item, TKey key) =>
            selectGroup(item, key)
                .DistinctUntilChanged()
                .Synchronize(locker!)
                .Do(
                    onNext: groupKey => grouper!.AddOrUpdate(key, groupKey, item, !parentUpdate ? observer : null),
                    onError: observer.OnError);

        // Create a shared connection to the source
        var shared = source
            .Synchronize(locker)
            .Do(_ => parentUpdate = true)
            .Publish();

        // First process the changesets
        var subChanges = shared
            .SubscribeSafe(
                onNext: changeSet => grouper.ProcessChangeSet(changeSet),
                onError: observer.OnError);

        // Next process the Grouping observables created for each item
        var subMergeMany = shared
            .MergeMany(CreateGroupObservable)
            .SubscribeSafe(
                onError: observer.OnError,
                onCompleted: observer.OnCompleted);

        // Finally, emit the results
        var subResults = shared
            .SubscribeSafe(
                onNext: _ =>
                {
                    grouper.EmitChanges(observer);
                    parentUpdate = false;
                },
                onError: observer.OnError);

        return new CompositeDisposable(shared.Connect(), subMergeMany, subChanges, grouper);
    });
}
