// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the SpecifiedGrouper class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="groupSelector">The groupSelector value.</param>
/// <param name="resultGroupSource">The resultGroupSource value.</param>
internal sealed class SpecifiedGrouper<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelector, IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The _groupSelector field.
    /// </summary>
    private readonly Func<TObject, TGroupKey> _groupSelector = groupSelector ?? throw new ArgumentNullException(nameof(groupSelector));

    /// <summary>
    /// The _resultGroupSource field.
    /// </summary>
    private readonly IObservable<IDistinctChangeSet<TGroupKey>> _resultGroupSource = resultGroupSource ?? throw new ArgumentNullException(nameof(resultGroupSource));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();

                // create source group cache
                var sourceGroups = _source.SynchronizeSafe(queue).Group(_groupSelector).DisposeMany().AsObservableCache();

                // create parent groups
                var parentGroups = _resultGroupSource.SynchronizeSafe(queue).Transform(
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

                return new CompositeDisposable(notifier, sourceGroups, parentGroups, updatesFromChildren, queue);
            });
}
