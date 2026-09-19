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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Applies a logical AND (intersection) between multiple list changeset streams.
    /// Only items present in ALL sources appear in the result.
    /// </summary>
    /// <typeparam name="T">The type of items in the lists.</typeparam>
    /// <param name="source">The first source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to intersect.</param>
    /// <param name="others">The additional <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> changeset streams to intersect with.</param>
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
    /// <seealso><c>Or&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>Except&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>Xor&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>ObservableCacheEx.And&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;[])</c></seealso>
    public static IObservable<IChangeSet<T>> And<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(others);

        return source.Combine(CombineOperator.And, others);
    }

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">A <c>ICollection&lt;T&gt;</c> of changeset streams to intersect.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>This overload accepts a pre-built collection of sources instead of a params array.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">An <c>IObservableList&lt;T&gt;</c> of changeset streams. Sources can be added or removed dynamically.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>This overload supports dynamic source management: adding or removing changeset streams from the observable list triggers re-evaluation.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">An <c>IObservableList&lt;IObservableList&lt;T&gt;&gt;</c> of <c>IObservableList&lt;IObservableList&lt;T&gt;&gt;</c>. Each inner list's changes are connected automatically.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>This overload accepts <c>IObservableList&lt;T&gt;</c> instances directly, calling <c>Connect()</c> internally.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">An <c>IObservableList&lt;ISourceList&lt;T&gt;&gt;</c> of <c>ISourceList&lt;T&gt;</c>. Each inner list's changes are connected automatically.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>This overload accepts <c>ISourceList&lt;T&gt;</c> instances directly, calling <c>Connect()</c> internally.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);
}
