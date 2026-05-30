// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOnObservable<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> selectGroup)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() =>
        Observable.Using(
            resourceFactory: () => new DynamicGrouper<TObject, TKey, TGroupKey>(),
            observableFactory: grouper => source.AggregateMany<TObject, TKey, (TGroupKey GroupKey, TObject Item), IGroupChangeSet<TObject, TKey, TGroupKey>>(
                onSourceChangeSet: (changes, track) =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        grouper.ProcessChange(change);

                        switch (change.Reason)
                        {
                            case ChangeReason.Add or ChangeReason.Update:
                                var item = change.Current;
                                var key = change.Key;
                                track(key, selectGroup(item, key).DistinctUntilChanged().Select(groupKey => (groupKey, item)));
                                break;

                            case ChangeReason.Remove:
                                track(change.Key, null);
                                break;
                        }
                    }
                },
                onInner: (value, parentKey) => grouper.AddOrUpdate(parentKey, value.GroupKey, value.Item),
                emit: observer => grouper.EmitChanges(observer)));
}
