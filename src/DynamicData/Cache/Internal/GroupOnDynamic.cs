// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOnDynamic<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TKey, TGroupKey>> selectGroupObservable, IObservable<Unit>? regrouper = null)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(observer =>
    {
        var dynamicGrouper = new DynamicGrouper<TObject, TKey, TGroupKey>();
        var notGrouped = new Cache<TObject, TKey>();
        var hasSelector = false;

        // Create shared observables for the 3 inputs
        var sharedSource = source.Synchronize(dynamicGrouper).Publish();
        var sharedGroupSelector = selectGroupObservable.DistinctUntilChanged().Synchronize(dynamicGrouper).Publish();
        var sharedRegrouper = (regrouper ?? Observable.Empty<Unit>()).Synchronize(dynamicGrouper).Publish();

        // The first value from the Group Selector should update the Grouper with all the values seen so far
        // Then indicate a selector has been found.  Subsequent values should just update the group selector.
        var subGroupSelector = sharedGroupSelector
            .SubscribeSafe(
                onNext: groupSelector =>
                {
                    if (hasSelector)
                    {
                        dynamicGrouper.SetGroupSelector(groupSelector, observer);
                    }
                    else
                    {
                        dynamicGrouper.Initialize(notGrouped.KeyValues, groupSelector, observer);
                        hasSelector = true;
                    }
                },
                onError: observer.OnError);

        // Ignore values until a selector has been provided
        // Then re-evaluate all the groupings each time it fires
        var subRegrouper = sharedRegrouper
            .SubscribeSafe(
                onNext: _ =>
                {
                    if (hasSelector)
                    {
                        dynamicGrouper.RegroupAll(observer);
                    }
                },
                onError: observer.OnError);

        var subChanges = sharedSource
            .SubscribeSafe(
                onNext: changeSet =>
                {
                    if (hasSelector)
                    {
                        dynamicGrouper.ProcessChangeSet(changeSet, observer);
                    }
                    else
                    {
                        notGrouped.Clone(changeSet);
                    }
                },
                onError: observer.OnError);

        // Create an observable that completes when all 3 inputs complete so the downstream can be completed as well
        var subOnComplete = Observable.Merge(sharedSource.ToUnit(), sharedGroupSelector.ToUnit(), sharedRegrouper)
            .SubscribeSafe(observer.OnError, observer.OnCompleted);

        return new CompositeDisposable(
            sharedGroupSelector.Connect(),
            sharedSource.Connect(),
            sharedRegrouper.Connect(),
            dynamicGrouper,
            subChanges,
            subGroupSelector,
            subRegrouper,
            subOnComplete);
    });
}
