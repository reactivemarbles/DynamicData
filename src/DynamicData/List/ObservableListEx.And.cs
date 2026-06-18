// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
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
}
