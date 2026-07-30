// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif
#if REACTIVE_SHIM
using DynamicData.Reactive.List.Internal;
#else
using DynamicData.List.Internal;
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
    /// Sorts the list using the specified comparer, maintaining a sorted output that incrementally updates as items change.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to sort.</param>
    /// <param name="comparer">The <c>IComparer&lt;T&gt;</c> used for sorting.</param>
    /// <param name="options">The <see cref="SortOptions.UseBinarySearch"/> for improved performance when sorted values are immutable.</param>
    /// <param name="resort">An optional <c>IObservable&lt;Unit&gt;</c> of <see cref="Unit"/> that forces a full re-sort when it fires. Required when sorted property values are mutable.</param>
    /// <param name="comparerChanged">An optional <c>IObservable&lt;IComparer&lt;T&gt;&gt;</c> of <c>IComparer&lt;T&gt;</c> that replaces the comparer, triggering a full re-sort.</param>
    /// <param name="resetThreshold">When the number of changes exceeds this threshold, a full reset is performed instead of incremental updates. Default is 50.</param>
    /// <returns>A list changeset stream with items in sorted order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comparer"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Maintains an internal sorted list. Each incoming change is applied incrementally: adds are inserted at the correct sorted position,
    /// removes are removed by index, and refreshes re-evaluate position (emitting <b>Moved</b> if changed).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Inserted at the correct sorted position. May trigger a full reset if the count exceeds <paramref name="resetThreshold"/>.</description></item>
    /// <item><term><b>Replace</b></term><description>Old item removed, new item inserted at sorted position.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Removed from sorted list.</description></item>
    /// <item><term><b>Refresh</b></term><description>Sort position re-evaluated. If position changed, a <b>Moved</b> is emitted.</description></item>
    /// <item><term>Comparer changed</term><description>Full re-sort of all items.</description></item>
    /// <item><term>Re-sort signal</term><description>Full re-sort using the current comparer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> <see cref="SortOptions.UseBinarySearch"/> is faster but requires that the values being sorted on never mutate. If they do, use the <paramref name="resort"/> signal or <c>AutoRefresh&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c>.</para>
    /// </remarks>
    /// <seealso><c>Sort&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IComparer&lt;T&gt;&gt;, SortOptions, IObservable&lt;Unit&gt;?, int)</c></seealso>
    /// <seealso><c>Page&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IPageRequest&gt;)</c></seealso>
    /// <seealso><c>Bind&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservableCollection&lt;T&gt;, BindingOptions)</c></seealso>
    /// <seealso><c>ObservableCacheEx.Sort&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IComparer&lt;TObject&gt;, SortOptimisations, int)</c></seealso>
    public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source, IComparer<T> comparer, SortOptions options = SortOptions.None, IObservable<Unit>? resort = null, IObservable<IComparer<T>>? comparerChanged = null, int resetThreshold = 50)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(comparer);

        return new Sort<T>(source, comparer, options, resort, comparerChanged, resetThreshold).Run();
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Sorts the list using an observable comparer. The initial comparer is taken from the first emission; subsequent emissions trigger a full re-sort.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>Until <paramref name="comparerChanged"/> emits its first comparer, items are sorted using <c>Comparer&lt;T&gt;.Default</c>. Downstream still receives changesets immediately; the initial ordering is whatever <c>Comparer&lt;T&gt;.Default</c> produces, then a full re-sort happens once the first comparer arrives.</para>
    /// </remarks>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to sort.</param>
    /// <param name="comparerChanged">An <c>IObservable&lt;IComparer&lt;T&gt;&gt;</c> of <c>IComparer&lt;T&gt;</c> that emits comparers. The first emission provides the initial sort order; subsequent emissions trigger re-sorts.</param>
    /// <param name="options"><see cref="SortOptions"/> for controlling sort behavior.</param>
    /// <param name="resort">An optional <c>IObservable&lt;Unit&gt;</c> of <see cref="Unit"/> to force a re-sort with the current comparer.</param>
    /// <param name="resetThreshold">The threshold for triggering a full reset instead of incremental updates.</param>
    public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source, IObservable<IComparer<T>> comparerChanged, SortOptions options = SortOptions.None, IObservable<Unit>? resort = null, int resetThreshold = 50)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(comparerChanged);

        return new Sort<T>(source, null, options, resort, comparerChanged, resetThreshold).Run();
    }
}
