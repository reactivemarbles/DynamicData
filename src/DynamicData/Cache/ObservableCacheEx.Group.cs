// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// ObservableCache extensions for grouping operators.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Groups items from the source changeset, producing groups only for group keys present in <paramref name="resultGroupSource"/>.
    /// Useful for parent-child relationships where parents and children come from different streams.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group.</param>
    /// <param name="groupSelector">The <see cref="Func{TObject, TGroupKey}"/> group selector factory.</param>
    /// <param name="resultGroupSource">An <see cref="IObservable{IDistinctChangeSet{TGroupKey}}"/> of <see cref="IDistinctChangeSet{TGroupKey}"/> used to determine which groups appear in the result.</param>
    /// <remarks>
    /// Useful for parent-child collection when the parent and child are soured from different streams.
    /// </remarks>
    /// <returns>An observable which will emit group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelector, IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelector.ThrowArgumentNullExceptionIfNull(nameof(groupSelector));
        resultGroupSource.ThrowArgumentNullExceptionIfNull(nameof(resultGroupSource));

        return new SpecifiedGrouper<TObject, TKey, TGroupKey>(source, groupSelector, resultGroupSource).Run();
    }

    /// <summary>
    /// Groups items from the source changeset by a key extracted via <paramref name="groupSelectorKey"/>.
    /// Each group is an observable sub-cache that receives changes for its members.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> that extracts the group key from each item.</param>
    /// <returns>An observable that emits group changesets. Each group exposes a sub-cache of its members.</returns>
    /// <remarks>
    /// <para>
    /// Items are assigned to groups based on the value returned by <paramref name="groupSelectorKey"/>.
    /// Groups are created on demand when the first item is assigned, and removed when their last member is removed.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The group key is evaluated. The item is added to the corresponding group (creating the group if new). An <b>Add</b> is emitted to the group's sub-cache.</description></item>
    /// <item><term>Update</term><description>The group key is re-evaluated. If unchanged, an <b>Update</b> is emitted within the same group. If the key changed, the item is removed from the old group (emitting <b>Remove</b>) and added to the new group (emitting <b>Add</b>). An empty old group is removed.</description></item>
    /// <item><term>Remove</term><description>The item is removed from its group. If the group becomes empty, the group itself is removed from the output.</description></item>
    /// <item><term>Refresh</term><description>The group key is re-evaluated. If unchanged, a <b>Refresh</b> is forwarded within the group. If the key changed, the item moves between groups (Remove from old, Add to new).</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Each group is a live sub-cache that can be subscribed to independently. Subscribers
    /// to a group receive only changes for items in that group. When a group is removed (becomes empty),
    /// its sub-cache completes.
    /// </para>
    /// </remarks>
    /// <seealso cref="GroupWithImmutableState{TObject, TKey, TGroupKey}"/>
    /// <seealso cref="GroupOnObservable{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{TGroupKey}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupOnProperty{TObject, TKey, TGroupKey}"/>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

        return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, null).Run();
    }

    /// <inheritdoc cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> that extracts the group key from each item.</param>
    /// <param name="regrouper">An <see cref="IObservable{Unit}"/> that, when it emits, all items are re-evaluated against the group selector, potentially moving items between groups.</param>
    /// <returns>An observable that emits group changesets.</returns>
    /// <remarks>This overload adds a <paramref name="regrouper"/> signal. When it fires, every item in the cache is re-grouped using the current selector, which is useful when the grouping depends on mutable item state.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit> regrouper)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));
        regrouper.ThrowArgumentNullExceptionIfNull(nameof(regrouper));

        return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }

    /// <summary>
    /// Groups items using a dynamically changing group selector function.
    /// Each time <paramref name="groupSelectorKeyObservable"/> emits a new selector, all items are re-grouped.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group.</param>
    /// <param name="groupSelectorKeyObservable">The <see cref="IObservable{Func{TObject, TKey, TGroupKey}}"/> that emits group selector functions. Each emission triggers a full re-grouping of all items.</param>
    /// <param name="regrouper">An <see cref="IObservable{Unit}"/> that optional signal to force re-evaluation of all items against the current selector.</param>
    /// <returns>An observable that emits group changesets.</returns>
    /// <remarks>
    /// <para>
    /// Unlike the static-selector overload, this accepts an observable of selector functions. When a new selector
    /// arrives, every item is re-evaluated and may move between groups. The optional <paramref name="regrouper"/>
    /// signal triggers re-evaluation without changing the selector (useful when item properties that affect grouping change).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The current selector determines the group. Item is added to the group (group created if new).</description></item>
    /// <item><term>Update</term><description>Group key re-evaluated. Item may move between groups if the key changed.</description></item>
    /// <item><term>Remove</term><description>Item removed from its group. Empty groups are removed.</description></item>
    /// <item><term>Refresh</term><description>Group key re-evaluated. Item may move between groups.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <seealso cref="GroupOnObservable{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{TGroupKey}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TKey, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKeyObservable.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKeyObservable));

        return new GroupOnDynamic<TObject, TKey, TGroupKey>(source, groupSelectorKeyObservable, regrouper).Run();
    }

    /// <inheritdoc cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{Func{TObject, TKey, TGroupKey}}, IObservable{Unit}?)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group.</param>
    /// <param name="groupSelectorKeyObservable">The <see cref="IObservable{Func{TObject, TGroupKey}}"/> of selector functions that take only the item (not the key).</param>
    /// <param name="regrouper">An optional <see cref="IObservable{Unit}"/> signal to force re-evaluation.</param>
    /// <remarks>This overload accepts a selector that does not receive the key. Delegates to the overload accepting <c>Func&lt;TObject, TKey, TGroupKey&gt;</c>.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        groupSelectorKeyObservable.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKeyObservable));

        return source.Group(groupSelectorKeyObservable.Select(AdaptSelector<TObject, TKey, TGroupKey>), regrouper);
    }

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

    /// <summary>
    /// <para>Groups the source using the property specified by the property selector. Groups are re-applied when the property value changed.</para>
    /// <para>When there are likely to be a large number of group property changes specify a throttle to improve performance.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group by a property value.</param>
    /// <param name="propertySelector">The <see cref="Expression{Func{TObject, TGroupKey}}"/> property selector used to group the items.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> a time span that indicates the throttle to wait for property change events.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable which will emit immutable group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnProperty<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TGroupKey>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnProperty<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// <para>Groups the source using the property specified by the property selector. Each update produces immutable grouping. Groups are re-applied when the property value changed.</para>
    /// <para>When there are likely to be a large number of group property changes specify a throttle to improve performance.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group by a property value with immutable snapshots.</param>
    /// <param name="propertySelector">The <see cref="Expression{Func{TObject, TGroupKey}}"/> property selector used to group the items.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> a time span that indicates the throttle to wait for property change events.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable which will emit immutable group change sets.</returns>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnPropertyWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TGroupKey>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnPropertyWithImmutableState<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Groups items by <paramref name="groupSelectorKey"/>, emitting immutable group snapshots instead of mutable sub-caches.
    /// Each group change contains a frozen copy of the group's state at that point in time.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group with immutable snapshots.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> that extracts the group key from each item.</param>
    /// <param name="regrouper">An <see cref="IObservable{Unit}"/> that optional signal to force re-evaluation of all items against the group selector.</param>
    /// <returns>An observable that emits immutable group changesets.</returns>
    /// <remarks>
    /// <para>
    /// Behaves identically to <see cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// in terms of how items are assigned to groups, but each group emission is an immutable snapshot.
    /// This makes it safe for parallel processing and eliminates race conditions on group state.
    /// The tradeoff is higher memory usage, since each change produces a new snapshot of the affected group.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item added to its group. An immutable snapshot of the group is emitted.</description></item>
    /// <item><term>Update</term><description>If group key unchanged, group snapshot re-emitted. If changed, item moves between groups; both affected groups emit new snapshots.</description></item>
    /// <item><term>Remove</term><description>Item removed from group. Updated snapshot emitted. Empty groups are removed.</description></item>
    /// <item><term>Refresh</term><description>Group key re-evaluated. If changed, item moves; affected group snapshots emitted.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <seealso cref="GroupOnPropertyWithImmutableState{TObject, TKey, TGroupKey}"/>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

        return new GroupOnImmutable<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }

    // TODO: Apply the Adapter to more places
    private static Func<TObject, TKey, TResult> AdaptSelector<TObject, TKey, TResult>(Func<TObject, TResult> other)
        where TObject : notnull
        where TKey : notnull
        where TResult : notnull => (obj, _) => other(obj);
}
