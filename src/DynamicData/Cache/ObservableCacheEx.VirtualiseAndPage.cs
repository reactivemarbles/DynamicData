// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Cache.Internal;

namespace DynamicData;

/// <summary>
/// ObservableCache extensions for the virtualised group of operators.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <inheritdoc cref="SortAndVirtualize{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, IObservable{IVirtualRequest}, SortAndVirtualizeOptions)"/>
    /// <remarks>This overload uses default <see cref="SortAndVirtualizeOptions"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> SortAndVirtualize<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        IObservable<IVirtualRequest> virtualRequests)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndVirtualize(comparer, virtualRequests, new SortAndVirtualizeOptions());

    /// <inheritdoc cref="SortAndVirtualize{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IComparer{TObject}}, IObservable{IVirtualRequest}, SortAndVirtualizeOptions)"/>
    /// <remarks>This overload uses default <see cref="SortAndVirtualizeOptions"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> SortAndVirtualize<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IObservable<IComparer<TObject>> comparerChanged,
        IObservable<IVirtualRequest> virtualRequests)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        virtualRequests.ThrowArgumentNullExceptionIfNull(nameof(virtualRequests));

        return source.SortAndVirtualize(comparerChanged, virtualRequests, new SortAndVirtualizeOptions());
    }

    /// <summary>
    /// Sorts unsorted data using <paramref name="comparer"/>, then returns only items within the
    /// virtual window defined by <paramref name="virtualRequests"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset stream.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="virtualRequests">The virtualizing requests (start index and page size).</param>
    /// <param name="options">Additional optimization options for virtualization.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <remarks>
    /// <para>
    /// Combines sorting and index-based windowing. Only items within the current virtual window are emitted.
    /// Use the observable comparer overload if you need to change sort order at runtime.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the new item's sorted position falls within the window, an <b>Add</b> is emitted. Items pushed out of the window produce a <b>Remove</b>.</description></item>
    /// <item><term>Update</term><description>If the updated item is within the window, an <b>Update</b> is emitted. Sort position changes may cause items to enter or leave the window.</description></item>
    /// <item><term>Remove</term><description>If the removed item was within the window, a <b>Remove</b> is emitted. Items shifted into the window produce an <b>Add</b>.</description></item>
    /// <item><term>Refresh</term><description>Sort position is re-evaluated. Window membership may change.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No data is emitted until <paramref name="virtualRequests"/> produces its first value. Changing the window can cause a full recalculation of visible items.</para>
    /// </remarks>
    /// <seealso cref="SortAndVirtualize{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IComparer{TObject}}, IObservable{IVirtualRequest}, SortAndVirtualizeOptions)"/>
    /// <seealso cref="Top{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, int)"/>
    public static IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> SortAndVirtualize<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        IObservable<IVirtualRequest> virtualRequests,
        SortAndVirtualizeOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        virtualRequests.ThrowArgumentNullExceptionIfNull(nameof(virtualRequests));

        return new SortAndVirtualize<TObject, TKey>(source, comparer, virtualRequests, options).Run();
    }

    /// <summary>
    /// Sorts unsorted data, then returns only the items within the virtual window defined by
    /// <paramref name="virtualRequests"/> (start index + size). Re-sorts when the comparer observable emits.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset stream.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>
    /// <param name="virtualRequests">The virtualizing requests (start index and page size).</param>
    /// <param name="options">Additional optimization options for virtualization.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <remarks>
    /// <para>
    /// Combines sorting and index-based windowing in a single operator. Only items within the
    /// current virtual window are emitted downstream. The window is defined by a start index and size.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the new item's sorted position falls within the window, an <b>Add</b> is emitted. Items pushed out of the window produce a <b>Remove</b>.</description></item>
    /// <item><term>Update</term><description>If the updated item is within the window, an <b>Update</b> is emitted. Sort position changes may cause items to enter or leave the window.</description></item>
    /// <item><term>Remove</term><description>If the removed item was within the window, a <b>Remove</b> is emitted. Items shifted into the window produce an <b>Add</b>.</description></item>
    /// <item><term>Refresh</term><description>Sort position is re-evaluated. Window membership may change.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No data is emitted until both the comparer observable and virtualRequests have produced their first values. Changing the window or comparer can cause a full recalculation of visible items.</para>
    /// </remarks>
    /// <seealso cref="SortAndPage{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IComparer{TObject}}, IObservable{IPageRequest}, SortAndPageOptions)"/>
    /// <seealso cref="Top{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, int)"/>
    public static IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> SortAndVirtualize<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IObservable<IComparer<TObject>> comparerChanged,
        IObservable<IVirtualRequest> virtualRequests,
        SortAndVirtualizeOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        virtualRequests.ThrowArgumentNullExceptionIfNull(nameof(virtualRequests));

        return new SortAndVirtualize<TObject, TKey>(source, comparerChanged, virtualRequests, options).Run();
    }

    /// <summary>
    /// Virtualises the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the source changeset stream.</param>
    /// <param name="virtualRequests"><see cref="IObservable{T}"/> the virtualising requests.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    [Obsolete(Constants.VirtualizeIsObsolete)]
    public static IObservable<IVirtualChangeSet<TObject, TKey>> Virtualise<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IVirtualRequest> virtualRequests)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        virtualRequests.ThrowArgumentNullExceptionIfNull(nameof(virtualRequests));

        return new Virtualise<TObject, TKey>(source, virtualRequests).Run();
    }

    /// <summary>
    /// Returns the top <paramref name="size"/> items from the source, sorted by <paramref name="comparer"/>.
    /// Equivalent to <c>SortAndVirtualize</c> with a fixed window starting at index 0.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the source changeset stream.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> the comparer.</param>
    /// <param name="size">The maximum number of items to return.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <exception cref="ArgumentOutOfRangeException">size;Size should be greater than zero.</exception>
    /// <remarks>
    /// <para>
    /// Internally delegates to <see cref="SortAndVirtualize{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, IObservable{IVirtualRequest}, SortAndVirtualizeOptions)"/>
    /// with a fixed <see cref="VirtualRequest"/> of <c>(0, size)</c>.
    /// </para>
    /// <para><b>Worth noting:</b> When the Nth item is displaced by a new item with higher sort priority, the displaced item is emitted as a <b>Remove</b> and the new item as an <b>Add</b>.</para>
    /// </remarks>
    /// <seealso cref="SortAndVirtualize{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, IObservable{IVirtualRequest}, SortAndVirtualizeOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> Top<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, int size)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");
        }

        return source.SortAndVirtualize(comparer, Observable.Return(new VirtualRequest(0, size)));
    }

    /// <summary>
    /// Limits the size of the result set to the specified number.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the source changeset stream.</param>
    /// <param name="size">The size.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <exception cref="ArgumentOutOfRangeException">size;Size should be greater than zero.</exception>
    [Obsolete(Constants.TopIsObsolete)]
    public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, int size)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");
        }

        return new Virtualise<TObject, TKey>(source, Observable.Return(new VirtualRequest(0, size))).Run();
    }

    /// <inheritdoc cref="SortAndPage{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, IObservable{IPageRequest}, SortAndPageOptions)"/>
    /// <remarks>This overload uses default <see cref="SortAndPageOptions"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> SortAndPage<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        IObservable<IPageRequest> pageRequests)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndPage(comparer, pageRequests, new SortAndPageOptions());

    /// <inheritdoc cref="SortAndPage{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IComparer{TObject}}, IObservable{IPageRequest}, SortAndPageOptions)"/>
    /// <remarks>This overload uses default <see cref="SortAndPageOptions"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> SortAndPage<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IObservable<IComparer<TObject>> comparerChanged,
        IObservable<IPageRequest> pageRequests)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pageRequests.ThrowArgumentNullExceptionIfNull(nameof(pageRequests));

        return source.SortAndPage(comparerChanged, pageRequests, new SortAndPageOptions());
    }

    /// <summary>
    /// Sorts unsorted data using <paramref name="comparer"/>, then pages the result using
    /// <paramref name="pageRequests"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset stream.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="pageRequests">The page requests (page number and page size).</param>
    /// <param name="options">Additional optimization options for paging.</param>
    /// <returns>An observable which will emit paged change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <remarks>
    /// <para>
    /// Combines sorting and page-based windowing. Only items on the current page are emitted.
    /// Use the observable comparer overload if you need to change sort order at runtime.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the new item's sorted position falls on the current page, an <b>Add</b> is emitted. Items pushed off the page produce a <b>Remove</b>.</description></item>
    /// <item><term>Update</term><description>If the updated item is on the current page, an <b>Update</b> is emitted. Sort position changes may move items on or off the page.</description></item>
    /// <item><term>Remove</term><description>If the removed item was on the current page, a <b>Remove</b> is emitted. Items shifted onto the page produce an <b>Add</b>.</description></item>
    /// <item><term>Refresh</term><description>Sort position is re-evaluated. Page membership may change.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No data is emitted until <paramref name="pageRequests"/> produces its first value. Page numbers are 1-based. Requesting a page beyond the data range results in an empty page.</para>
    /// </remarks>
    /// <seealso cref="SortAndPage{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IComparer{TObject}}, IObservable{IPageRequest}, SortAndPageOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> SortAndPage<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        IObservable<IPageRequest> pageRequests,
        SortAndPageOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pageRequests.ThrowArgumentNullExceptionIfNull(nameof(pageRequests));

        return new SortAndPage<TObject, TKey>(source, comparer, pageRequests, options).Run();
    }

    /// <summary>
    /// Sorts unsorted data, then pages the result using page number and page size from
    /// <paramref name="pageRequests"/>. Re-sorts when the comparer observable emits.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset stream.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>
    /// <param name="pageRequests">The page requests (page number and page size).</param>
    /// <param name="options">Additional optimization options for paging.</param>
    /// <returns>An observable which will emit paged change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <remarks>
    /// <para>
    /// Combines sorting and page-based windowing in a single operator. Only items on the current page
    /// are emitted downstream. The page is defined by a 1-based page number and page size.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the new item's sorted position falls on the current page, an <b>Add</b> is emitted. Items pushed off the page produce a <b>Remove</b>.</description></item>
    /// <item><term>Update</term><description>If the updated item is on the current page, an <b>Update</b> is emitted. Sort position changes may move items on or off the page.</description></item>
    /// <item><term>Remove</term><description>If the removed item was on the current page, a <b>Remove</b> is emitted. Items shifted onto the page produce an <b>Add</b>.</description></item>
    /// <item><term>Refresh</term><description>Sort position is re-evaluated. Page membership may change.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No data is emitted until both the comparer observable and pageRequests have produced their first values. Page numbers are 1-based. Requesting a page beyond the data range results in an empty page.</para>
    /// </remarks>
    /// <seealso cref="SortAndVirtualize{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IComparer{TObject}}, IObservable{IVirtualRequest}, SortAndVirtualizeOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> SortAndPage<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IObservable<IComparer<TObject>> comparerChanged,
        IObservable<IPageRequest> pageRequests,
        SortAndPageOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pageRequests.ThrowArgumentNullExceptionIfNull(nameof(pageRequests));

        return new SortAndPage<TObject, TKey>(source, comparerChanged, pageRequests, options).Run();
    }

    /// <summary>
    /// Returns the page as specified by the pageRequests observable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source"><see cref="IObservable{T}"/> the source changeset stream.</param>
    /// <param name="pageRequests"><see cref="IObservable{T}"/> the page requests.</param>
    /// <returns>An observable which emits change sets.</returns>
    [Obsolete(Constants.PageIsObsolete)]
    public static IObservable<IPagedChangeSet<TObject, TKey>> Page<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IPageRequest> pageRequests)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pageRequests.ThrowArgumentNullExceptionIfNull(nameof(pageRequests));

        return new Page<TObject, TKey>(source, pageRequests).Run();
    }
}
