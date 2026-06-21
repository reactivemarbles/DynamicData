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
    /// Filters items using a per-item <c>IObservable&lt;Boolean&gt;</c> that controls inclusion.
    /// Each item's observable is created by <paramref name="filterFactory"/> and toggles the item in or out of the downstream stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to filter using per-item observables.</param>
    /// <param name="filterFactory">A factory that creates an <c>IObservable&lt;Boolean&gt;</c> for each item and its key. When the observable emits <see langword="true"/>, the item is included; when <see langword="false"/>, it is excluded.</param>
    /// <param name="buffer">A <see cref="TimeSpan"/> that optional time window to buffer inclusion changes from per-item observables before re-evaluating.</param>
    /// <param name="scheduler">An <see cref="IScheduler"/> that optional scheduler used for buffering.</param>
    /// <returns>An observable changeset containing only items whose per-item observable most recently emitted <see langword="true"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Source changeset handling (parent events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the per-item observable. The item is <b>not included downstream until the observable emits its first <see langword="true"/></b>.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's observable subscription and subscribes to the new item's observable. Inclusion state is reset; the new observable must emit before the item reappears.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's observable subscription. If the item was included downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the item is currently included downstream. Otherwise dropped.</description></item>
    /// </list>
    /// <para>
    /// <b>Per-item observable handling (filter observable events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Emission</term><description>Behavior</description></listheader>
    /// <item><term>First <see langword="true"/></term><description>The item is included: an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term><see langword="false"/> (was included)</term><description>The item is excluded: a <b>Remove</b> is emitted downstream.</description></item>
    /// <item><term><see langword="true"/> (was excluded)</term><description>The item is re-included: an <b>Add</b> is emitted downstream.</description></item>
    /// <item><term><see langword="true"/> (was included)</term><description>No effect (already included).</description></item>
    /// <item><term><see langword="false"/> (was excluded)</term><description>No effect (already excluded).</description></item>
    /// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
    /// <item><term>Completed</term><description>The item remains in its current inclusion state. No further toggling is possible for this item.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Items are invisible downstream until their per-item observable emits at least one <see langword="true"/>.
    /// If an item's observable never emits, the item never appears. The <paramref name="buffer"/> parameter batches
    /// rapid inclusion changes from per-item observables into a single re-evaluation, reducing changeset chatter.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="filterFactory"/> is <see langword="null"/>.</exception>
    /// <seealso><c>Filter&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, bool&gt;, bool)</c></seealso>
    /// <seealso><c>ObservableListEx.FilterOnObservable</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(filterFactory);

        return new FilterOnObservable<TObject, TKey>(source, filterFactory, buffer, scheduler).Run();
    }

    /// <summary>
    /// Provides an overload of <c>FilterOnObservable</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="filterFactory">The filterFactory value.</param>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// This overload does not provide the key to <paramref name="filterFactory"/>; only the item is passed.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> FilterOnObservable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(filterFactory);

        return source.FilterOnObservable((obj, _) => filterFactory(obj), buffer, scheduler);
    }
}
