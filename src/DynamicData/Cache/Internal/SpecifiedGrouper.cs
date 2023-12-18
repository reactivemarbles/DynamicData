// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class SpecifiedGrouper<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelector, IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    private readonly Func<TObject, TGroupKey> _groupSelector = groupSelector ?? throw new ArgumentNullException(nameof(groupSelector));

    private readonly IObservable<IDistinctChangeSet<TGroupKey>> _resultGroupSource = resultGroupSource ?? throw new ArgumentNullException(nameof(resultGroupSource));

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(
            observer =>
            {
                var locker = new object();

                // create source group cache
                var sourceGroups = _source.Synchronize(locker).Group(_groupSelector).DisposeMany().AsObservableCache();

                // create parent groups
                var parentGroups = _resultGroupSource.Synchronize(locker).Transform(
                    x =>
                    {
                        // if child already has data, populate it.
                        var result = new ManagedGroup<TObject, TKey, TGroupKey>(x);
                        var child = sourceGroups.Lookup(x);
                        if (child.HasValue)
                        {
                            // dodgy cast but fine as a groups is always a ManagedGroup;
                            var group = (ManagedGroup<TObject, TKey, TGroupKey>)child.Value;
                            result.Update(updater => updater.Clone(group.GetInitialUpdates()));
                        }

                        return result;
                    }).DisposeMany().AsObservableCache();

                // connect to each individual item and update the resulting group
                var updatesFromChildren = sourceGroups.Connect().SubscribeMany(
                    x => x.Cache.Connect().Subscribe(
                        updates =>
                        {
                            var groupToUpdate = parentGroups.Lookup(x.Key);
                            if (groupToUpdate.HasValue)
                            {
                                groupToUpdate.Value.Update(updater => updater.Clone(updates));
                            }
                        })).DisposeMany().Subscribe();

                var notifier = parentGroups.Connect().Select(
                    x =>
                    {
                        var groups = x.Select(s => new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(s.Reason, s.Key, s.Current));
                        return new GroupChangeSet<TObject, TKey, TGroupKey>(groups);
                    }).SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        notifier.Dispose();
                        sourceGroups.Dispose();
                        parentGroups.Dispose();
                        updatesFromChildren.Dispose();
                    });
            });
}
