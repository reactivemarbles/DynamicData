// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to group.</param>
    /// <param name="groupSelector">The <c>Func&lt;TObject, TGroupKey&gt;</c> group selector factory.</param>
    /// <param name="resultGroupSource">An <c>IObservable&lt;IDistinctChangeSet&lt;TGroupKey&gt;&gt;</c> of <c>IDistinctChangeSet&lt;TGroupKey&gt;</c> used to determine which groups appear in the result.</param>
    /// <remarks>
    /// Useful for parent-child collection when the parent and child are soured from different streams.
    /// </remarks>
    /// <returns>An observable which will emit group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelector, IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(groupSelector);
        ArgumentExceptionHelper.ThrowIfNull(resultGroupSource);

        return new SpecifiedGrouper<TObject, TKey, TGroupKey>(source, groupSelector, resultGroupSource).Run();
    }

    /// <summary>
    /// Groups items from the source changeset by a key extracted via <paramref name="groupSelectorKey"/>.
    /// Each group is an observable sub-cache that receives changes for its members.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to group.</param>
    /// <param name="groupSelectorKey">A <c>Func&lt;T, TResult&gt;</c> that extracts the group key from each item.</param>
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
    /// <seealso><c>GroupWithImmutableState&lt;TObject, TKey, TGroupKey&gt;</c></seealso>
    /// <seealso><c>GroupOnObservable&lt;TObject, TKey, TGroupKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TKey, IObservable&lt;TGroupKey&gt;&gt;, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>GroupOnProperty&lt;TObject, TKey, TGroupKey&gt;</c></seealso>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(groupSelectorKey);

        return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, null).Run();
    }

    /// <summary>
    /// Provides an overload of <c>Group</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to group.</param>
    /// <param name="groupSelectorKey">A <c>Func&lt;T, TResult&gt;</c> that extracts the group key from each item.</param>
    /// <param name="regrouper">An <c>IObservable&lt;Unit&gt;</c> that, when it emits, all items are re-evaluated against the group selector, potentially moving items between groups.</param>
    /// <returns>An observable that emits group changesets.</returns>
    /// <remarks>This overload adds a <paramref name="regrouper"/> signal. When it fires, every item in the cache is re-grouped using the current selector, which is useful when the grouping depends on mutable item state.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit> regrouper)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(groupSelectorKey);
        ArgumentExceptionHelper.ThrowIfNull(regrouper);

        return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }

    /// <summary>
    /// Groups items using a dynamically changing group selector function.
    /// Each time <paramref name="groupSelectorKeyObservable"/> emits a new selector, all items are re-grouped.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to group.</param>
    /// <param name="groupSelectorKeyObservable">The <c>IObservable&lt;Func&lt;TObject, TKey, TGroupKey&gt;&gt;</c> that emits group selector functions. Each emission triggers a full re-grouping of all items.</param>
    /// <param name="regrouper">An <c>IObservable&lt;Unit&gt;</c> that optional signal to force re-evaluation of all items against the current selector.</param>
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
    /// <seealso><c>Group&lt;TObject, TKey, TGroupKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TGroupKey&gt;)</c></seealso>
    /// <seealso><c>GroupOnObservable&lt;TObject, TKey, TGroupKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TKey, IObservable&lt;TGroupKey&gt;&gt;, TimeSpan?, IScheduler?)</c></seealso>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TKey, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(groupSelectorKeyObservable);

        return new GroupOnDynamic<TObject, TKey, TGroupKey>(source, groupSelectorKeyObservable, regrouper).Run();
    }

    /// <summary>
    /// Provides an overload of <c>Group</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to group.</param>
    /// <param name="groupSelectorKeyObservable">The <c>IObservable&lt;Func&lt;TObject, TGroupKey&gt;&gt;</c> of selector functions that take only the item (not the key).</param>
    /// <param name="regrouper">An optional <c>IObservable&lt;Unit&gt;</c> signal to force re-evaluation.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts a selector that does not receive the key. Delegates to the overload accepting <c>Func&lt;TObject, TKey, TGroupKey&gt;</c>.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, TGroupKey>> groupSelectorKeyObservable, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(groupSelectorKeyObservable);

        return source.Group(groupSelectorKeyObservable.Select(AdaptSelector<TObject, TKey, TGroupKey>), regrouper);
    }
}
