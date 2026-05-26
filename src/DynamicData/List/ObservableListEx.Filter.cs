// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableList extensions for filtering and change-reason gating.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Extracts distinct values from source items using <paramref name="valueSelector"/>, with reference counting to track when values enter and leave the result set.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TValue">The type of distinct values produced.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to extract distinct values.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}"/> function that extracts the value to track from each source item.</param>
    /// <returns>A list changeset stream of distinct values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Maintains an internal reference count per distinct value. A value is included when its count first exceeds zero
    /// and removed when its count drops back to zero.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Value extracted. If first occurrence, an <b>Add</b> is emitted. Otherwise the reference count is incremented silently.</description></item>
    /// <item><term><b>Replace</b></term><description>Old value's reference count decremented (removed if zero), new value's count incremented (added if first). If the value did not change, no emission.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b></term><description>Reference count decremented. If the count reaches zero, a <b>Remove</b> is emitted for that distinct value.</description></item>
    /// <item><term><b>Refresh</b></term><description>Value is re-extracted. If changed, old value decremented and new value incremented (same as Replace logic).</description></item>
    /// <item><term><b>Clear</b></term><description>All reference counts cleared. <b>Remove</b> emitted for every tracked distinct value.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ObservableCacheEx.DistinctValues{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TValue})"/>
    public static IObservable<IChangeSet<TValue>> DistinctValues<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TValue> valueSelector)
        where TObject : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return new Distinct<TObject, TValue>(source, valueSelector).Run();
    }

    /// <summary>
    /// Filters items from the source list changeset stream using a static predicate.
    /// Only items satisfying <paramref name="predicate"/> are included downstream.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to filter.</param>
    /// <param name="predicate">A <see cref="Func{T, TResult}"/> predicate that determines which items are included. Items returning <see langword="true"/> appear downstream; items returning <see langword="false"/> are excluded.</param>
    /// <returns>A list changeset stream containing only items that satisfy <paramref name="predicate"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Use this overload when you need only a single predicate function for the lifetime of the subscription;
    /// unlike the dynamic-predicate and state-driven overloads, the predicate function itself never changes.
    /// Note that this does not mean an item's inclusion is fixed: Refresh events can re-evaluate each item against the predicate
    /// and promote a previously-excluded item to included (or vice versa).
    /// Item ordering is preserved.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The predicate is evaluated. If the item passes, an <b>Add</b> is emitted at the calculated downstream index. Otherwise dropped.</description></item>
    /// <item><term>AddRange</term><description>Each item in the range is evaluated. Matching items are emitted as an <b>AddRange</b>.</description></item>
    /// <item><term>Replace</term><description>The predicate is re-evaluated. Four outcomes: both pass produces <b>Replace</b>; new passes but old didn't produces <b>Add</b>; old passed but new doesn't produces <b>Remove</b>; neither passes is dropped.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>RemoveRange</term><description>Included items in the range are emitted as individual <b>Remove</b> changes.</description></item>
    /// <item><term>Refresh</term><description>The predicate is re-evaluated. If the item now passes but previously did not, an <b>Add</b> is emitted. If it previously passed but no longer does, a <b>Remove</b> is emitted. If still passes, the <b>Refresh</b> is forwarded. If still fails, dropped.</description></item>
    /// <item><term>Clear</term><description>All downstream items are cleared.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Refresh events trigger re-evaluation, which can promote or demote items (turning a Refresh into an Add or Remove). Pair with <see cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/> for property-change-driven filtering.</para>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, IObservable{Func{T, bool}}, ListFilterPolicy)"/>
    /// <seealso cref="FilterOnObservable{TObject}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/>
    public static IObservable<IChangeSet<T>> Filter<T>(
                this IObservable<IChangeSet<T>> source,
                Func<T, bool> predicate)
            where T : notnull
        => List.Internal.Filter.Static<T>.Create(
            source: source,
            predicate: predicate,
            suppressEmptyChangesets: true);

    /// <summary>
    /// Filters items using a dynamically changing predicate.
    /// When <paramref name="predicate"/> emits a new function, all items are re-evaluated.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to filter.</param>
    /// <param name="predicate">An <see cref="IObservable{Func{T, bool}}"/> that emits new predicate functions. Each emission triggers a full re-evaluation of all items.</param>
    /// <param name="filterPolicy">The <see cref="ListFilterPolicy"/> that controls re-filtering behavior when the predicate changes.</param>
    /// <returns>A list changeset stream containing only items that satisfy the most recent predicate.</returns>
    /// <remarks>
    /// <para>
    /// Each time <paramref name="predicate"/> emits, every item is re-evaluated against the new predicate.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The current predicate is evaluated. If the item passes, an <b>Add</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>AddRange</term><description>Each item is evaluated. Matching items are emitted as <b>AddRange</b>.</description></item>
    /// <item><term>Replace</term><description>Re-evaluated. Same four-outcome logic as the static overload (Replace, Add, Remove, or dropped).</description></item>
    /// <item><term>Remove</term><description>If the item was downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description>Re-evaluated. If inclusion status changed, an <b>Add</b> or <b>Remove</b> is emitted. If unchanged, <b>Refresh</b> forwarded or dropped.</description></item>
    /// <item><term>Clear</term><description>All downstream items are cleared.</description></item>
    /// <item><term>Predicate changed</term><description>All items are re-evaluated against the new predicate. The output is shaped by <paramref name="filterPolicy"/>.</description></item>
    /// <item><term>OnCompleted</term><description>Independent completion of <paramref name="predicate"/> does not terminate the filter.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No items are included until <paramref name="predicate"/> emits its first function.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="FilterOnObservable{TObject}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, IObservable<Func<T, bool>> predicate, ListFilterPolicy filterPolicy = ListFilterPolicy.CalculateDiff)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new List.Internal.Filter.Dynamic<T>(source, predicate, filterPolicy).Run();
    }

    /// <summary>
    /// Filters items using a predicate that receives external state. When <paramref name="predicateState"/> emits a new state value,
    /// all items are re-evaluated against <paramref name="predicate"/> using the updated state.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <typeparam name="TState">The type of state value required by <paramref name="predicate"/>.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to filter.</param>
    /// <param name="predicateState">An <see cref="IObservable{TState}"/> stream of state values to be passed to <paramref name="predicate"/>.</param>
    /// <param name="predicate">A static <see cref="Func{T, TResult}"/> predicate receiving the current state and an item, returning <see langword="true"/> to include or <see langword="false"/> to exclude. The function itself does not change; only the state value passed to it changes.</param>
    /// <param name="filterPolicy">The <see cref="ListFilterPolicy"/> that controls re-filtering behavior when the state changes.</param>
    /// <param name="suppressEmptyChangeSets">When <see langword="true"/> (default), empty changesets are suppressed. Set to <see langword="false"/> to publish empty changesets (useful for monitoring loading status).</param>
    /// <returns>A list changeset stream containing only items satisfying <paramref name="predicate"/> with the current state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="predicateState"/>, or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The predicate cannot be invoked until the first state value is received. Until then, all items are treated as excluded.
    /// Each subsequent state emission triggers a full re-evaluation of all items according to <paramref name="filterPolicy"/>.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Evaluated using current state. Matching items emitted as <b>Add</b>/<b>AddRange</b>.</description></item>
    /// <item><term><b>Replace</b></term><description>Re-evaluated. Same four-outcome logic as the static filter (Replace, Add, Remove, or dropped).</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b></term><description>If the item was downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Refresh</b></term><description>Re-evaluated against current state. Inclusion status may change.</description></item>
    /// <item><term><b>Clear</b></term><description>All downstream items are cleared.</description></item>
    /// <item><term>State changed</term><description>All items are re-evaluated with the new state value. The output is shaped by <paramref name="filterPolicy"/>.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, IObservable{Func{T, bool}}, ListFilterPolicy)"/>
    public static IObservable<IChangeSet<T>> Filter<T, TState>(
                this IObservable<IChangeSet<T>> source,
                IObservable<TState> predicateState,
                Func<TState, T, bool> predicate,
                ListFilterPolicy filterPolicy = ListFilterPolicy.CalculateDiff,
                bool suppressEmptyChangeSets = true)
            where T : notnull
        => List.Internal.Filter.WithPredicateState<T, TState>.Create(
            source: source,
            predicateState: predicateState,
            predicate: predicate,
            filterPolicy: filterPolicy,
            suppressEmptyChangeSets: suppressEmptyChangeSets);

    /// <summary>
    /// Filters each item using a per-item <see cref="IObservable{T}"/> of <see cref="bool"/> that dynamically controls inclusion.
    /// When an item's observable emits <see langword="true"/> the item enters the result; when it emits <see langword="false"/> the item is removed.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to filter by property value.</param>
    /// <param name="objectFilterObservable">A function that returns an observable of <see cref="bool"/> for each item, controlling its inclusion.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration applied to each per-item observable to reduce re-evaluation frequency.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling. Defaults to the system default scheduler.</param>
    /// <returns>A list changeset stream containing only items whose per-item observable most recently emitted <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="objectFilterObservable"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Each item in the source gets its own subscription to the observable returned by <paramref name="objectFilterObservable"/>.
    /// The item's inclusion is determined by the most recent boolean value emitted by that observable.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the per-item observable. Item is included when it first emits <see langword="true"/>.</description></item>
    /// <item><term><b>Replace</b></term><description>Old subscription disposed, new subscription created for the replacement item.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Subscription disposed. If the item was downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if the item is currently included.</description></item>
    /// </list>
    /// <list type="table">
    /// <listheader><term>Event (per-item observable)</term><description>Behavior</description></listheader>
    /// <item><term>Emits <see langword="true"/></term><description>If not already included, an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term>Emits <see langword="false"/></term><description>If currently included, a <b>Remove</b> is emitted downstream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, IObservable{Func{T, bool}}, ListFilterPolicy)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<TObject>> FilterOnObservable<TObject>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<bool>> objectFilterObservable, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new FilterOnObservable<TObject>(source, objectFilterObservable, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Filters items based on a property value, automatically re-evaluating when the specified property changes on any item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to filter by property value.</param>
    /// <param name="propertySelector"><see cref="Expression{TDelegate}"/> selecting the property to monitor for changes.</param>
    /// <param name="predicate">A <see cref="Func{T, TResult}"/> predicate evaluated against the item to determine inclusion.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of items satisfying the predicate, re-evaluated on property changes.</returns>
    /// <remarks>
    /// <para>Deprecated. Use <see cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/> followed by <see cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/> instead.</para>
    /// </remarks>
    /// <seealso cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
    public static IObservable<IChangeSet<TObject>> FilterOnProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new FilterOnProperty<TObject, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Suppresses all <see cref="ListChangeReason.Refresh"/> changes from the stream. All other change reasons pass through.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to strip refresh events.</param>
    /// <returns>A list changeset stream with Refresh changes removed.</returns>
    /// <seealso cref="WhereReasonsAreNot{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> SuppressRefresh<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.WhereReasonsAreNot(ListChangeReason.Refresh);

    /// <summary>
    /// Filters the changeset stream to include only changes with the specified <see cref="ListChangeReason"/> values.
    /// Index information is stripped from the output because removing some changes invalidates the original index positions.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to filter by change reason.</param>
    /// <param name="reasons">The <see cref="ListChangeReason"/> change reasons to include. Must specify at least one.</param>
    /// <returns>A list changeset stream containing only changes with the specified reasons.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reasons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reasons"/> is empty.</exception>
    /// <remarks>
    /// <para>Filters individual changes within each changeset. If filtering removes all changes from a changeset, the empty changeset is suppressed via <see cref="NotEmpty{T}(IObservable{IChangeSet{T}})"/>.</para>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes can cause downstream operators to accumulate items indefinitely (memory leak). Index information is stripped because removing some changes invalidates the original index positions.</para>
    /// </remarks>
    /// <seealso cref="WhereReasonsAreNot{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> WhereReasonsAre<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            changes =>
            {
                var filtered = changes.Where(change => matches.Contains(change.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }

    /// <summary>
    /// Filters the changeset stream to exclude changes with the specified <see cref="ListChangeReason"/> values.
    /// Index information is stripped from the output because removing some changes invalidates the original index positions.
    /// The exception is when only <see cref="ListChangeReason.Refresh"/> is excluded, since removing Refresh does not affect index calculations.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to filter by excluding change reasons.</param>
    /// <param name="reasons">The <see cref="ListChangeReason"/> change reasons to exclude. Must specify at least one.</param>
    /// <returns>A list changeset stream with the specified change reasons removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reasons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reasons"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// Empty changesets (after filtering) are automatically suppressed. When only <see cref="ListChangeReason.Refresh"/> is excluded,
    /// indices are preserved, since removing Refresh does not affect index calculations.
    /// </para>
    /// </remarks>
    /// <seealso cref="WhereReasonsAre{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> WhereReasonsAreNot<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        if (reasons.Length == 1 && reasons[0] == ListChangeReason.Refresh)
        {
            // If only refresh changes are removed, then there's no need to remove the indexes
            return source.Select(changes =>
            {
                var filtered = changes.Where(c => c.Reason != ListChangeReason.Refresh);
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            updates =>
            {
                var filtered = updates.Where(u => !matches.Contains(u.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }
}
