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
                .Synchronize(locker!)
                .Do(
                    groupKey => grouper!.AddOrUpdate(item, key, groupKey, !parentUpdate ? observer : null),
                    observer.OnError);

        // Create a shared connection to the source
        var shared = source
            .Synchronize(locker)
            .Do(_ => parentUpdate = true)
            .Publish();

        // For each item, subscribe to the grouping observable and update that entry whenever it fires
        var subMergeMany = shared
            .MergeMany(CreateGroupObservable)
            .SubscribeSafe(observer.OnError);

        // Give the group a chance to handle/emit any other changes
        var subChanges = shared
            .SubscribeSafe(
                changeSet =>
                {
                    grouper.ProcessChanges(changeSet, observer);
                    parentUpdate = false;
                },
                observer.OnError,
                observer.OnCompleted);

        return new CompositeDisposable(shared.Connect(), subMergeMany, subChanges);
    });
}
