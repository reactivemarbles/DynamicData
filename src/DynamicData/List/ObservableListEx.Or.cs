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
}
