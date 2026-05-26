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
/// ObservableCache extensions for filtering and change-reason gating.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Validates that each changeset contains no duplicate keys.
    /// If duplicates are detected, an <see cref="InvalidOperationException"/> is emitted via <c>OnError</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to validate for unique keys.</param>
    /// <returns>A changeset stream guaranteed to contain unique keys per changeset.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Forwarded as <b>Add</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Update</term><description>Forwarded as <b>Update</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Remove</term><description>Forwarded as <b>Remove</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the key is unique within the changeset.</description></item>
    /// <item><term>OnError</term><description>Also emitted with <see cref="InvalidOperationException"/> if duplicate keys are detected in a changeset.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> EnsureUniqueKeys<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new UniquenessEnforcer<TObject, TKey>(source).Run();
    }

    /// <summary>
    /// Filters items from the source changeset stream using a static predicate.
    /// Only items that satisfy <paramref name="filter"/> are included downstream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter.</param>
    /// <param name="filter">The <see cref="Func{TObject, bool}"/> predicate used to determine whether each item is included.</param>
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
    /// <para><b>Worth noting:</b> <b>Refresh</b> events trigger re-evaluation, which can promote or demote items. Pair with <see cref="AutoRefresh{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/> for property-change-driven filtering.</para>
    /// </remarks>
    /// <seealso cref="FilterImmutable{TObject, TKey}"/>
    /// <seealso cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableListEx.Filter"/>
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

    /// <inheritdoc cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{Func{TObject, bool}}, IObservable{Unit}, bool)"/>
    /// <remarks>
    /// This overload does not accept a <c>reapplyFilter</c> signal. It is equivalent to calling the
    /// full dynamic overload with <see cref="Observable.Empty{TResult}()"/> as the reapply observable.
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
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter.</param>
    /// <param name="predicateState">The <see cref="IObservable{TState}"/> stream of state values to be passed to <paramref name="predicate"/>.</param>
    /// <param name="predicate">The <see cref="Func{TState, TObject, bool}"/> predicate that receives the current state and an item, returning <see langword="true"/> to include or <see langword="false"/> to exclude.</param>
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
    /// <item><term>Update</term><description>Re-evaluated. Four outcomes as with the static <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> overload.</description></item>
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

    /// <inheritdoc cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter.</param>
    /// <param name="predicateChanged">The <see cref="IObservable{Func{TObject, bool}}"/> that emits new predicates. Each emission replaces the current predicate and triggers a full re-evaluation of all items.</param>
    /// <param name="reapplyFilter">The <see cref="IObservable{Unit}"/> that, when it emits, triggers a full re-evaluation of all items against the current predicate. Useful when filtering on mutable item properties.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed for performance.</param>
    /// <remarks>
    /// In addition to the per-item behavior described in the static <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> overload,
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

    /// <summary>
    /// Creates a filtered stream, optimized for stateless/deterministic filtering of immutable items.
    /// </summary>
    /// <typeparam name="TObject">The type of collection items to be filtered.</typeparam>
    /// <typeparam name="TKey">The type of the key values of each collection item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter (items assumed immutable).</param>
    /// <param name="predicate">The <see cref="Func{TObject, bool}"/> filtering predicate to be applied to each item.</param>
    /// <param name="suppressEmptyChangeSets">A flag indicating whether the created stream should emit empty changesets. Empty changesets are suppressed by default, for performance. Set to ensure that a downstream changeset occurs for every upstream changeset.</param>
    /// <returns>A stream of collection changesets where upstream collection items are filtered by the given predicate.</returns>
    /// <remarks>
    /// <para>The goal of this operator is to optimize a common use-case of reactive programming, where data values flowing through a stream are immutable, and state changes are distributed by publishing new immutable items as replacements, instead of mutating the items directly.</para>
    /// <para>In addition to assuming that all collection items are immutable, this operator also assumes that the given filter predicate is deterministic, such that the result it returns will always be the same each time a specific input is passed to it. In other words, the predicate itself also contains no mutable state.</para>
    /// <para>Under these assumptions, this operator can bypass the need to keep track of every collection item that passes through it, which the normal <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> operator must do, in order to re-evaluate the filtering status of items, during a refresh operation.</para>
    /// <para>Consider using this operator when the following are true:</para>
    /// <list type="bullet">
    /// <item><description>Your collection items are immutable, and changes are published by replacing entire items</description></item>
    /// <item><description>Your filtering logic does not change over the lifetime of the stream, only the items do</description></item>
    /// <item><description>Your filtering predicate runs quickly, and does not heavily allocate memory</description></item>
    /// </list>
    /// <para>Note that, because filtering is purely deterministic, Refresh operations are transparently ignored by this operator.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The predicate is evaluated. If it passes, an <b>Add</b> is emitted. Otherwise the item is dropped.</description></item>
    /// <item><term>Update</term><description>Four outcomes: if both old and new values pass, an <b>Update</b> is emitted. If only the new value passes, an <b>Add</b> is emitted. If only the old value passed, a <b>Remove</b> is emitted. If neither passes, the change is dropped.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description><b>Dropped.</b> Because items are assumed immutable, there is nothing to re-evaluate.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> FilterImmutable<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, bool> predicate,
            bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new FilterImmutable<TObject, TKey>(
                predicate: predicate,
                source: source,
                suppressEmptyChangeSets: suppressEmptyChangeSets)
            .Run();
    }

    /// <summary>
    /// Filters items using a per-item <see cref="IObservable{Boolean}"/> that controls inclusion.
    /// Each item's observable is created by <paramref name="filterFactory"/> and toggles the item in or out of the downstream stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter using per-item observables.</param>
    /// <param name="filterFactory">A factory that creates an <see cref="IObservable{Boolean}"/> for each item and its key. When the observable emits <see langword="true"/>, the item is included; when <see langword="false"/>, it is excluded.</param>
    /// <param name="buffer">A <see cref="TimeSpan"/> that optional time window to buffer inclusion changes from per-item observables before re-evaluating.</param>
    /// <param name="scheduler">An <see cref="IScheduler"/> that optional scheduler used for buffering.</param>
    /// <returns>An observable changeset containing only items whose per-item observable most recently emitted <see langword="true"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Source changeset handling (parent events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the per-item observable. The item is <b>not included downstream until the observable emits its first <see langword="true"/></b>.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's observable subscription and subscribes to the new item's observable. Inclusion state is reset; the new observable must emit before the item reappears.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's observable subscription. If the item was included downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the item is currently included downstream. Otherwise dropped.</description></item>
    /// </list>
    /// <para>
    /// <b>Per-item observable handling (filter observable events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Emission</term><description>Behavior</description></listheader>
    /// <item><term>First <see langword="true"/></term><description>The item is included: an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term><see langword="false"/> (was included)</term><description>The item is excluded: a <b>Remove</b> is emitted downstream.</description></item>
    /// <item><term><see langword="true"/> (was excluded)</term><description>The item is re-included: an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term><see langword="true"/> (was included)</term><description>No effect (already included).</description></item>
    /// <item><term><see langword="false"/> (was excluded)</term><description>No effect (already excluded).</description></item>
    /// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
    /// <item><term>Completed</term><description>The item remains in its current inclusion state. No further toggling is possible for this item.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Items are invisible downstream until their per-item observable emits at least one <see langword="true"/>.
    /// If an item's observable never emits, the item never appears. The <paramref name="buffer"/> parameter batches
    /// rapid inclusion changes from per-item observables into a single re-evaluation, reducing changeset chatter.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="filterFactory"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/>
    /// <seealso cref="ObservableListEx.FilterOnObservable"/>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        filterFactory.ThrowArgumentNullExceptionIfNull(nameof(filterFactory));

        return new FilterOnObservable<TObject, TKey>(source, filterFactory, buffer, scheduler).Run();
    }

    /// <inheritdoc cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <remarks>
    /// This overload does not provide the key to <paramref name="filterFactory"/>; only the item is passed.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        filterFactory.ThrowArgumentNullExceptionIfNull(nameof(filterFactory));

        return source.FilterOnObservable((obj, _) => filterFactory(obj), buffer, scheduler);
    }

    private static IObservable<Func<TSource, TKey, bool>>? ForForced<TSource, TKey>(this IObservable<Unit>? source)
        where TKey : notnull => source?.Select(
            _ =>
            {
                static bool Transformer(TSource item, TKey key) => true;
                return (Func<TSource, TKey, bool>)Transformer;
            });

    private static IObservable<Func<TSource, TKey, bool>>? ForForced<TSource, TKey>(this IObservable<Func<TSource, bool>>? source)
        where TKey : notnull => source?.Select(
            condition =>
            {
                bool Transformer(TSource item, TKey key) => condition(item);
                return (Func<TSource, TKey, bool>)Transformer;
            });

    /// <summary>
    /// Ignores updates when the update is the same reference.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to suppress same-reference updates in.</param>
    /// <returns>An observable which emits change sets and ignores equal value changes.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> IgnoreSameReferenceUpdate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.IgnoreUpdateWhen((c, p) => ReferenceEquals(c, p));

    /// <summary>
    /// Ignores the update when the condition is met.
    /// The first parameter in the ignore function is the current value and the second parameter is the previous value.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to selectively suppress updates in.</param>
    /// <param name="ignoreFunction">The <see cref="Func{TObject, TObject, bool}"/> ignore function (current,previous)=>{ return true to ignore }.</param>
    /// <returns>An observable which emits change sets and ignores updates equal to the lambda.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> IgnoreUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject, bool> ignoreFunction)
        where TObject : notnull
        where TKey : notnull => source.Select(
            updates =>
            {
                var result = updates.Where(
                    u =>
                    {
                        if (u.Reason != ChangeReason.Update)
                        {
                            return true;
                        }

                        return !ignoreFunction(u.Current, u.Previous.Value);
                    });
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();

    /// <summary>
    /// Only includes the update when the condition is met.
    /// The first parameter in the ignore function is the current value and the second parameter is the previous value.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to selectively include updates in.</param>
    /// <param name="includeFunction">The <see cref="Func{TObject, TObject, bool}"/> include function (current,previous)=>{ return true to include }.</param>
    /// <returns>An observable which emits change sets and ignores updates equal to the lambda.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> IncludeUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TObject, bool> includeFunction)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        includeFunction.ThrowArgumentNullExceptionIfNull(nameof(includeFunction));

        return source.Select(
            changes =>
            {
                var result = changes.Where(change => change.Reason != ChangeReason.Update || includeFunction(change.Current, change.Previous.Value));
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();
    }

    /// <summary>
    /// Filters out empty changesets from the stream. A thin wrapper around <c>Where(changes =&gt; changes.Count != 0)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to suppress empty changesets.</param>
    /// <returns>An observable that emits only non-empty changesets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="StartWithEmpty{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> NotEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(changes => changes.Count != 0);
    }

    /// <summary>
    /// Suppress refresh notifications.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to strip refresh events.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SuppressRefresh<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.WhereReasonsAreNot(ChangeReason.Refresh);

    /// <summary>
    /// Includes changes for the specified reasons only.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter by change reason.</param>
    /// <param name="reasons">The <see cref="ChangeReason"/> values to filter by.</param>
    /// <returns>An observable which emits a change set with items matching the reasons.</returns>
    /// <exception cref="ArgumentNullException">reasons.</exception>
    /// <exception cref="ArgumentException">Must select at least on reason.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes will cause memory leaks in downstream caches, since items are never cleaned up.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAre<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must select at least one reason");
        }

        var hashed = new HashSet<ChangeReason>(reasons);

        return source.Select(updates => new ChangeSet<TObject, TKey>(updates.Where(u => hashed.Contains(u.Reason)))).NotEmpty();
    }

    /// <summary>
    /// Excludes updates for the specified reasons.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter by excluding change reasons.</param>
    /// <param name="reasons">The <see cref="ChangeReason"/> values to filter by.</param>
    /// <returns>An observable which emits a change set with items not matching the reasons.</returns>
    /// <exception cref="ArgumentNullException">reasons.</exception>
    /// <exception cref="ArgumentException">Must select at least on reason.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes will cause memory leaks in downstream caches, since items are never cleaned up.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAreNot<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
        where TObject : notnull
        where TKey : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must select at least one reason");
        }

        var hashed = new HashSet<ChangeReason>(reasons);

        return source.Select(updates => new ChangeSet<TObject, TKey>(updates.Where(u => !hashed.Contains(u.Reason)))).NotEmpty();
    }
}
