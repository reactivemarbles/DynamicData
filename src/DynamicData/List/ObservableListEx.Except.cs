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
    /// Applies a logical set-difference (Except) between the source and other streams.
    /// Items present in the first source but not in any of the <paramref name="others"/> are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> from which other streams are subtracted.</param>
    /// <param name="others">The other <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> changeset streams to exclude from the result.</param>
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
    /// <para><b>Worth noting:</b> Unlike <c>Or&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c>, the first source is asymmetric: only its items can appear in the result.</para>
    /// </remarks>
    /// <seealso><c>And&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>Or&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>Xor&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>ObservableCacheEx.Except&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;[])</c></seealso>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(others);

        return source.Combine(CombineOperator.Except, others);
    }

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>Static overload accepting a pre-built collection of sources. The first item in the collection is the primary source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>Dynamic overload: sources can be added or removed from the <c>IObservableList&lt;T&gt;</c> at runtime. The first source in the list acts as the primary.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>Dynamic overload accepting <c>IObservableList&lt;T&gt;</c> of <c>IObservableList&lt;T&gt;</c>. Each inner list's <c>Connect()</c> is used as a source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Provides an overload of <c>Combine</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>Dynamic overload accepting <c>IObservableList&lt;T&gt;</c> of <c>ISourceList&lt;T&gt;</c>. Each inner list's <c>Connect()</c> is used as a source.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);
}
