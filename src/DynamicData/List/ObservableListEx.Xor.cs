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
    /// Applies a logical XOR (symmetric difference) between the source and other streams.
    /// Items present in exactly one source are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The primary source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to exclusively combine.</param>
    /// <param name="others">The other <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> changeset streams to combine with.</param>
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
    /// <seealso><c>And&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>Or&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>Except&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>ObservableCacheEx.Xor&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;[])</c></seealso>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(others);

        return source.Combine(CombineOperator.Xor, others);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Applies a logical XOR between a pre-built collection of list changeset sources.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Dynamic XOR: sources can be added or removed from the <c>IObservableList&lt;T&gt;</c> at runtime.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Dynamic XOR accepting <c>IObservableList&lt;T&gt;</c> of <c>IObservableList&lt;T&gt;</c>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Dynamic XOR accepting <c>IObservableList&lt;T&gt;</c> of <c>ISourceList&lt;T&gt;</c>. Each inner list's <c>Connect()</c> is used as a source.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);
}
