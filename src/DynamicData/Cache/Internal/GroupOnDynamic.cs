// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOnDynamic<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TKey, TGroupKey>> selectGroupObservable, IObservable<Unit>? regrouper = null)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(observer =>
    {
        var dynamicGrouper = new DynamicGrouper<TObject, TKey, TGroupKey>();
        var locker = new object();
        var parentUpdate = false;

        // Create a shared connection to the source
        var shared = source
            .Synchronize(locker)
            .Do(_ => parentUpdate = true)
            .Publish();

        var subGroupSelector = selectGroupObservable
            .Synchronize(locker)
            .SubscribeSafe(
                onNext: groupSelector => dynamicGrouper.SetGroupSelector(groupSelector, observer),
                onError: observer.OnError);

        var subRegrouper = (regrouper == null)
            ? Disposable.Empty
            : regrouper
                .Synchronize(locker)
                .SubscribeSafe(
                    onNext: _ => dynamicGrouper.RegroupAll(observer),
                    onError: observer.OnError);

        var subChanges = shared
            .SubscribeSafe(
                onNext: changeSet => dynamicGrouper.ProcessChangeSet(changeSet),
                onError: observer.OnError);

        // Next process the Grouping observables created for each item
        var subMergeMany = shared
            .MergeMany(CreateGroupObservable)
            .SubscribeSafe(onError: observer.OnError);

        // Finally, emit the results
        var subResults = shared
            .SubscribeSafe(
                onNext: _ =>
                {
                    dynamicGrouper.EmitChanges(observer);
                    parentUpdate = false;
                },
                onError: observer.OnError,
                onComplete: observer.OnCompleted);

        return new CompositeDisposable(shared.Connect(), subMergeMany, subChanges, subGroupSelector, subRegrouper, dynamicGrouper);
    });
}
