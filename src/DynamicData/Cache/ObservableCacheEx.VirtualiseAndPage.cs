// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
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
    /// <summary>
    /// Sort and virtualize the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="virtualRequests">The virtualizing requests.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> SortAndVirtualize<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        IObservable<IVirtualRequest> virtualRequests)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndVirtualize(comparer, virtualRequests, new SortAndVirtualizeOptions());

    /// <summary>
    /// Sort and virtualize the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>>
    /// <param name="virtualRequests">The virtualizing requests.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
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
    /// Sort and virtualize the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="virtualRequests">The virtualizing requests.</param>
    /// <param name="options"> Addition optimization options for virtualization.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
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
    /// Sort and virtualize the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>>
    /// <param name="virtualRequests">The virtualizing requests.</param>
    /// <param name="options"> Addition optimization options for virtualization.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
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
    /// <param name="source">The source.</param>
    /// <param name="virtualRequests">The virtualising requests.</param>
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
    /// Limits the size of the result set to the specified number, ordering by the comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer.</param>
    /// <param name="size">The size.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <exception cref="ArgumentOutOfRangeException">size;Size should be greater than zero.</exception>
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
    /// <param name="source">The source.</param>
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

    /// <summary>
    /// Sort and page the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="pageRequests">The virtualizing requests.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> SortAndPage<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        IObservable<IPageRequest> pageRequests)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndPage(comparer, pageRequests, new SortAndPageOptions());

    /// <summary>
    /// Sort and page the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>>
    /// <param name="pageRequests">The virtualizing requests.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
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
    /// Sort and page the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="pageRequests">The virtualizing requests.</param>
    /// <param name="options"> Addition optimization options for virtualization.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
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
    /// Sort and page the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>>
    /// <param name="pageRequests">The virtualizing requests.</param>
    /// <param name="options"> Addition optimization options for virtualization.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
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
    /// <param name="source">The source.</param>
    /// <param name="pageRequests">The page requests.</param>
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
