// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
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
        ArgumentExceptionHelper.ThrowIfNull(source);

        ArgumentExceptionHelper.ThrowIfNull(predicate);

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
}
