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
/// ObservableList extensions for set-style combinators (And, Or, Xor, Except).
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Applies a logical AND (intersection) between multiple list changeset streams.
    /// Only items present in ALL sources appear in the result.
    /// </summary>
    /// <typeparam name="T">The type of items in the lists.</typeparam>
    /// <param name="source">The first source <see cref="IObservable{IChangeSet{T}}"/> to intersect.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{T}}"/> changeset streams to intersect with.</param>
    /// <returns>A list changeset stream containing items that exist in every source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Uses reference counting per item across all sources. An item appears downstream only when
    /// its reference count is non-zero in ALL sources. Item identity is determined by the default equality comparer.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>The item's reference count is incremented in its source tracker. If the item is now present in all sources, an <b>Add</b> is emitted.</description></item>
    /// <item><term>Replace</term><description>The old item's reference count is decremented and the new item's is incremented. Depending on whether each is present in ALL sources, this emits an <b>Add</b>, <b>Remove</b>, <b>Replace</b>, or nothing.</description></item>
    /// <item><term>Remove/RemoveRange/Clear</term><description>The item's reference count is decremented. If it was in the result and is no longer in all sources, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the item is currently in the result.</description></item>
    /// <item><term>Moved</term><description>Ignored (set operations are position-independent).</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Item identity uses object equality, not position. Duplicate items in a single source are reference-counted independently.</para>
    /// </remarks>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.And{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    public static IObservable<IChangeSet<T>> And<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.And, others);
    }

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">A <see cref="ICollection{T}"/> of changeset streams to intersect.</param>
    /// <remarks>
    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>This overload accepts a pre-built collection of sources instead of a params array.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">An <see cref="IObservableList{T}"/> of changeset streams. Sources can be added or removed dynamically.</param>
    /// <remarks>
    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>This overload supports dynamic source management: adding or removing changeset streams from the observable list triggers re-evaluation.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">An <see cref="IObservableList{IObservableList{T}}"/> of <see cref="IObservableList{IObservableList{T}}"/>. Each inner list's changes are connected automatically.</param>
    /// <remarks>
    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>This overload accepts <see cref="IObservableList{T}"/> instances directly, calling <c>Connect()</c> internally.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <param name="sources">An <see cref="IObservableList{ISourceList{T}}"/> of <see cref="ISourceList{T}"/>. Each inner list's changes are connected automatically.</param>
    /// <remarks>
    /// <inheritdoc cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>This overload accepts <see cref="ISourceList{T}"/> instances directly, calling <c>Connect()</c> internally.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    private static IObservable<IChangeSet<T>> Combine<T>(this ICollection<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Combiner<T>(sources, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservable<IChangeSet<T>> source, CombineOperator type, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        if (others.Length == 0)
        {
            throw new ArgumentException("Must be at least one item to combine with", nameof(others));
        }

        var items = source.EnumerateOne().Union(others).ToList();
        return new Combiner<T>(items, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<ISourceList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservableList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new DynamicCombiner<T>(sources, type).Run();
    }

    /// <summary>
    /// Applies a logical set-difference (Except) between the source and other streams.
    /// Items present in the first source but not in any of the <paramref name="others"/> are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary <see cref="IObservable{IChangeSet{T}}"/> from which other streams are subtracted.</param>
    /// <param name="others">The other <see cref="IObservable{IChangeSet{T}}"/> changeset streams to exclude from the result.</param>
    /// <returns>A list changeset stream containing items from <paramref name="source"/> that are not in any of <paramref name="others"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Item identity is determined by the default equality comparer for <typeparamref name="T"/>. Across all sources, items are tracked
    /// by reference-counted equality (not by index position).
    /// The first source has a special role: only items from it can appear in the result, and only if they do not exist in any other source.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b> (first source)</term><description>If the item does not exist in any other source, an <b>Add</b> is emitted.</description></item>
    /// <item><term><b>Add</b>/<b>AddRange</b> (other source)</term><description>If the item was in the result (from first source), a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b> (first source)</term><description>If the item was in the result, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b> (other source)</term><description>If the item exists in the first source and no longer in any other, an <b>Add</b> is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Treated as a Remove of the old item plus an Add of the new item, with set logic re-evaluated.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored by the set logic (no positional semantics).</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if the item is currently in the result set.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Unlike <see cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>, the first source is asymmetric: only its items can appear in the result.</para>
    /// </remarks>
    /// <seealso cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.Except{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Except, others);
    }

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>Static overload accepting a pre-built collection of sources. The first item in the collection is the primary source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>Dynamic overload: sources can be added or removed from the <see cref="IObservableList{T}"/> at runtime. The first source in the list acts as the primary.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>Dynamic overload accepting <see cref="IObservableList{T}"/> of <see cref="IObservableList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <remarks>
    /// <inheritdoc cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <para>Dynamic overload accepting <see cref="IObservableList{T}"/> of <see cref="ISourceList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Applies a logical OR (union) between a pre-built collection of list changeset sources. Items present in any source are included.
    /// </summary>
    /// <seealso cref="ObservableCacheEx.Or{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    public static IObservable<IChangeSet<T>> Or<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Applies a logical OR (union) between the source and other list changeset streams.
    /// Items present in any of the sources are included in the result, using reference-counted equality.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary source <see cref="IObservable{IChangeSet{T}}"/> to union.</param>
    /// <param name="others">The other <see cref="IObservable{IChangeSet{T}}"/> changeset streams to combine with.</param>
    /// <returns>A list changeset stream containing items that exist in at least one source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Item identity is determined by the default equality comparer for <typeparamref name="T"/>. Uses reference-counted equality: an item is included when it first appears in any source and removed when it no longer exists in any source.
    /// <b>Moved</b> changes are ignored by the set logic.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b> (any source)</term><description>If the item is new to the result, an <b>Add</b> is emitted. Otherwise the reference count is incremented.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b> (any source)</term><description>Reference count decremented. If count reaches zero, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Old item reference count decremented, new item reference count incremented. Add/Remove emitted as needed.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if the item is in the result set.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Or, others);
    }

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic OR: sources can be added or removed from the <see cref="IObservableList{T}"/> at runtime.
    /// </summary>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic OR accepting <see cref="IObservableList{T}"/> of <see cref="IObservableList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <inheritdoc cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic OR accepting <see cref="IObservableList{T}"/> of <see cref="ISourceList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Applies a logical XOR (symmetric difference) between the source and other streams.
    /// Items present in exactly one source are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary source <see cref="IObservable{IChangeSet{T}}"/> to exclusively combine.</param>
    /// <param name="others">The other <see cref="IObservable{IChangeSet{T}}"/> changeset streams to combine with.</param>
    /// <returns>A list changeset stream containing items that exist in exactly one source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="others"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Item identity is determined by the default equality comparer for <typeparamref name="T"/>. Uses reference-counted equality: an item is included when it exists in exactly one source.
    /// If it appears in a second source, it is removed from the result. If it then leaves one source,
    /// it re-enters the result. <b>Moved</b> changes are ignored.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Reference count updated. If the item is now in exactly one source, an <b>Add</b> is emitted. If now in two or more, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Reference count decremented. If now in exactly one source, an <b>Add</b> is emitted. If now in zero, a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Old item reference count decremented, new item incremented, with Xor logic applied.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded if item is in the result set.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="And{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="Except{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.Xor{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Xor, others);
    }

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Applies a logical XOR between a pre-built collection of list changeset sources.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic XOR: sources can be added or removed from the <see cref="IObservableList{T}"/> at runtime.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic XOR accepting <see cref="IObservableList{T}"/> of <see cref="IObservableList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <inheritdoc cref="Xor{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <summary>
    /// Dynamic XOR accepting <see cref="IObservableList{T}"/> of <see cref="ISourceList{T}"/>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);
}
