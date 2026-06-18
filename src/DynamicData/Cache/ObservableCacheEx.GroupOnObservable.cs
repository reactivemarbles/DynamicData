// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Groups items where each item's group key is determined by a per-item observable.
    /// The observable is created by <paramref name="groupObservableSelector"/> for each item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group using per-item observables.</param>
    /// <param name="groupObservableSelector">A <see cref="Func{T, TResult}"/> factory that creates a group key observable for each item and its key.</param>
    /// <returns>An observable that emits group changesets. Each group is a live sub-cache of its members.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/> which evaluates
    /// the group key synchronously, this operator defers group assignment until the per-item observable emits.
    /// </para>
    /// <para>
    /// <b>Source changeset handling (parent events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the per-item group key observable. The item is <b>not placed in any group until the observable emits its first group key</b>.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's group key subscription and subscribes to the new item's observable. The item is removed from its current group until the new observable emits.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's group key subscription. The item is removed from its current group. Empty groups are removed.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The item remains in its current group.</description></item>
    /// </list>
    /// <para>
    /// <b>Per-item observable handling (group key observable events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Emission</term><description>Behavior</description></listheader>
    /// <item><term>First value</term><description>The item is placed into the group matching the emitted key. An <b>Add</b> appears in that group's sub-cache. If the group is new, the group itself is added to the output.</description></item>
    /// <item><term>New value (different key)</term><description>The item moves: <b>Remove</b> from the old group, <b>Add</b> to the new group. If the old group becomes empty, it is removed from the output.</description></item>
    /// <item><term>Same value (unchanged key)</term><description>No effect (filtered by <c>DistinctUntilChanged</c>).</description></item>
    /// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
    /// <item><term>Completed</term><description>The item remains in its current group. No further group key changes are possible for this item.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Items are invisible (not in any group) until their per-item observable emits at least one
    /// group key. If an item's observable never emits, the item never appears in any group. Per-item observable errors
    /// terminate the entire stream. The output completes when the source completes and all per-item observables have
    /// also completed.
    /// </para>
    /// </remarks>
    /// <seealso cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <seealso cref="GroupOnProperty{TObject, TKey, TGroupKey}"/>
    /// <seealso cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="TransformOnObservable{TSource, TKey, TDestination}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TKey, IObservable{TDestination}})"/>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnObservable<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> groupObservableSelector)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupObservableSelector.ThrowArgumentNullExceptionIfNull(nameof(groupObservableSelector));

        return new GroupOnObservable<TObject, TKey, TGroupKey>(source, groupObservableSelector).Run();
    }

    /// <summary>
    /// Groups the source by the latest value from their observable created by the given factory.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group using per-item observables.</param>
    /// <param name="groupObservableSelector">The <see cref="Func{TObject, IObservable{TGroupKey}}"/> group selector key.</param>
    /// <returns>An observable which will emit group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnObservable<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TGroupKey>> groupObservableSelector)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        groupObservableSelector.ThrowArgumentNullExceptionIfNull(nameof(groupObservableSelector));

        return source.GroupOnObservable(AdaptSelector<TObject, TKey, IObservable<TGroupKey>>(groupObservableSelector));
    }
}
