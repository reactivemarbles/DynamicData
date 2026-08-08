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
}
