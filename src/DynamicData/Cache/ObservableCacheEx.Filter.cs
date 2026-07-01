// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// Filters items from the source changeset stream using a static predicate.
    /// Only items that satisfy <paramref name="filter"/> are included downstream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to filter.</param>
    /// <param name="filter">The <c>Func&lt;TObject, bool&gt;</c> predicate used to determine whether each item is included.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed for performance. Set to <see langword="false"/> to emit empty changesets, which can be useful for monitoring loading status.</param>
    /// <returns>An observable changeset containing only items that satisfy <paramref name="filter"/>.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The predicate is evaluated. If it passes, an <b>Add</b> is emitted. Otherwise the item is dropped.</description></item>
    /// <item><term>Update</term><description>Four outcomes: if both old and new values pass, an <b>Update</b> is emitted. If only the new value passes, an <b>Add</b> is emitted. If only the old value passed, a <b>Remove</b> is emitted. If neither passes, the change is dropped.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description>The predicate is re-evaluated. If the item now passes but previously did not, an <b>Add</b> is emitted. If it still passes, a <b>Refresh</b> is forwarded. If it no longer passes, a <b>Remove</b> is emitted. If it still fails, the change is dropped.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> <b>Refresh</b> events trigger re-evaluation, which can promote or demote items. Pair with <c>AutoRefresh&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c> for property-change-driven filtering.</para>
    /// </remarks>
    /// <seealso><c>FilterImmutable&lt;TObject, TKey&gt;</c></seealso>
    /// <seealso><c>FilterOnObservable&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TKey, IObservable&lt;bool&gt;&gt;, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>ObservableListEx.Filter</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, bool> filter,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.Filter.Static<TObject, TKey>.Create(
            source: source,
            filter: filter,
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <summary>
    /// Provides an overload of <c>Filter</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="predicateChanged">The predicateChanged value.</param>
    /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// This overload does not accept a <c>reapplyFilter</c> signal. It is equivalent to calling the
    /// full dynamic overload with <c>Observable.Empty&lt;TResult&gt;()</c> as the reapply observable.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                IObservable<Func<TObject, bool>> predicateChanged,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull
        => source.Filter(
            predicateChanged: predicateChanged,
            reapplyFilter: Observable.Empty<Unit>(),
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <summary>
    /// Creates a dynamically filtered stream where the filter predicate depends on external state.
    /// Each emission from <paramref name="predicateState"/> triggers a full re-filtering of all items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TState">The type of state value required by <paramref name="predicate"/>.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to filter.</param>
    /// <param name="predicateState">The <c>IObservable&lt;TState&gt;</c> stream of state values to be passed to <paramref name="predicate"/>.</param>
    /// <param name="predicate">The <c>Func&lt;TState, TObject, bool&gt;</c> predicate that receives the current state and an item, returning <see langword="true"/> to include or <see langword="false"/> to exclude.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed for performance. Set to <see langword="false"/> to emit empty changesets.</param>
    /// <returns>An observable changeset containing only items satisfying <paramref name="predicate"/> for the latest state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="predicateState"/>, or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <paramref name="predicateState"/> should emit an initial value immediately upon subscription.
    /// Until the first state value arrives, no items pass the filter (all items are excluded).
    /// Each subsequent state emission triggers a full re-evaluation of every item in the collection.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Evaluated against the current state. If it passes, an <b>Add</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Update</term><description>Re-evaluated. Four outcomes as with the static <c>Filter&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, bool&gt;, bool)</c> overload.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description>Re-evaluated against the current state. May produce <b>Add</b>, <b>Refresh</b>, <b>Remove</b>, or be dropped.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> <paramref name="predicateState"/> should emit an initial value immediately. Each emission triggers a full re-evaluation of all items, which can be expensive for large collections.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey, TState>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                IObservable<TState> predicateState,
                Func<TState, TObject, bool> predicate,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.Filter.Dynamic<TObject, TKey, TState>.Create(
            source: source,
            predicateState: predicateState,
            predicate: predicate,
            reapplyFilter: Observable.Empty<Unit>(),
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <summary>
    /// Provides an overload of <c>Filter</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to filter.</param>
    /// <param name="predicateChanged">The <c>IObservable&lt;Func&lt;TObject, bool&gt;&gt;</c> that emits new predicates. Each emission replaces the current predicate and triggers a full re-evaluation of all items.</param>
    /// <param name="reapplyFilter">The <c>IObservable&lt;Unit&gt;</c> that, when it emits, triggers a full re-evaluation of all items against the current predicate. Useful when filtering on mutable item properties.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed for performance.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// In addition to the per-item behavior described in the static <c>Filter&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, bool&gt;, bool)</c> overload,
    /// emissions from <paramref name="predicateChanged"/> replace the predicate and trigger full re-filtering,
    /// while emissions from <paramref name="reapplyFilter"/> re-evaluate all items against the current predicate.
    /// <para><b>Worth noting:</b> No items are included until the predicate observable emits its first value.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                IObservable<Func<TObject, bool>> predicateChanged,
                IObservable<Unit> reapplyFilter,
                bool suppressEmptyChangeSets = true)
            where TObject : notnull
            where TKey : notnull

        => Cache.Internal.Filter.Dynamic<TObject, TKey, Func<TObject, bool>>.Create(
            source: source,
            predicateState: predicateChanged,
            predicate: static (predicate, item) => predicate.Invoke(item),
            reapplyFilter: reapplyFilter,
            suppressEmptyChangeSets: suppressEmptyChangeSets);
}
