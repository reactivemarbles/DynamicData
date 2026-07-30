// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    /// Applies page-based windowing to the source list. Only items within the current page (determined by page number and page size from <paramref name="requests"/>) are included downstream.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to page.</param>
    /// <param name="requests">An observable of <see cref="IPageRequest"/> controlling which page to display (page number and page size).</param>
    /// <returns>An <c>IPageChangeSet&lt;T&gt;</c> stream containing only items within the current page window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Maintains the full source list internally and calculates the page window on each change or page request.
    /// Items entering the page window produce <b>Add</b>; items leaving produce <b>Remove</b>. A new page request triggers
    /// a full recalculation of the page contents.
    /// </para>
    /// <para><b>Worth noting:</b> Duplicate items are removed from the result via <c>Distinct()</c> using the default equality comparer for <typeparamref name="T"/>, regardless of source order. The source should ideally be sorted before paging, since list order determines which items fall within each page window.</para>
    /// </remarks>
    /// <seealso><c>Virtualise&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IVirtualRequest&gt;)</c></seealso>
    /// <seealso><c>Top&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, int)</c></seealso>
    /// <seealso><c>Sort&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IComparer&lt;T&gt;, SortOptions, IObservable&lt;Unit&gt;?, IObservable&lt;IComparer&lt;T&gt;&gt;?, int)</c></seealso>
    public static IObservable<IPageChangeSet<T>> Page<T>(this IObservable<IChangeSet<T>> source, IObservable<IPageRequest> requests)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(requests);

        return new Pager<T>(source, requests).Run();
    }
}
