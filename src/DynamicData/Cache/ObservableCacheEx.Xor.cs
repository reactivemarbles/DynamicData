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
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Combines multiple changeset streams using logical XOR (symmetric difference).
    /// An item appears downstream only if it exists in exactly one source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to combine.</param>
    /// <param name="others">The additional <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> streams to combine with.</param>
    /// <returns>A changeset stream containing items present in exactly one source.</returns>
    /// <remarks>
    /// <para>
    /// Items are tracked via reference counting. An item appears downstream only when exactly one
    /// source holds it. Adding the same key from a second source removes it from the result;
    /// removing from that second source restores it.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the key is now held by exactly one source, an <b>Add</b> is emitted. If adding causes the count to reach 2+, a <b>Remove</b> is emitted (the item is no longer exclusive).</description></item>
    /// <item><term>Update</term><description>If the item is currently downstream (count is 1), an <b>Update</b> is emitted.</description></item>
    /// <item><term>Remove</term><description>Reference count decremented. If the count drops to exactly 1, an <b>Add</b> is emitted (the item is now exclusive to one source). If it drops to 0, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the item is downstream, a <b>Refresh</b> is forwarded.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="others"/> is <see langword="null"/>.</exception>
    /// <seealso><c>And&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;[])</c></seealso>
    /// <seealso><c>Or&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;[])</c></seealso>
    /// <seealso><c>Except&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;[])</c></seealso>
    /// <seealso><c>ObservableListEx.Xor</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (others is null || others.Length == 0)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Xor, others);
    }

    /// <summary>
    /// Provides an overload of <c>Xor</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="sources">The <c>ICollection&lt;T&gt;</c> of streams to combine.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts a pre-built collection of sources instead of a params array.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Xor);
    }

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are only in one of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <c>IObservableList&lt;T&gt;</c> of streams to combine.</param>
    /// <returns>An observable which emits a change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Xor);
    }

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <c>IObservableList&lt;IObservableCache&lt;TObject, TKey&gt;&gt;</c> of changeset streams to combine.</param>
    /// <returns>An observable which emits a change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Xor);
    }

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <c>IObservableList&lt;ISourceCache&lt;TObject, TKey&gt;&gt;</c> of changeset streams to combine.</param>
    /// <returns>An observable which emits a change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Xor);
    }
}
