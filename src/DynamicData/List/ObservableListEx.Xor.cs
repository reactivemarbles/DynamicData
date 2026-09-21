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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
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
