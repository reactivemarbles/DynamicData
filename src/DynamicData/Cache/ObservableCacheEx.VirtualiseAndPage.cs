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
    /// Virtualises the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="virtualRequests">The virtualising requests.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IVirtualChangeSet<TObject, TKey>> Virtualise<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IVirtualRequest> virtualRequests)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        virtualRequests.ThrowArgumentNullExceptionIfNull(nameof(virtualRequests));

        return new Virtualise<TObject, TKey>(source, virtualRequests).Run();
    }

    /// <summary>
    /// Virtualise the underlying data from the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="virtualRequests">The virtualising requests.</param>
    /// <returns>An observable which will emit virtual change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Virtualise<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, IObservable<IVirtualRequest> virtualRequests)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        virtualRequests.ThrowArgumentNullExceptionIfNull(nameof(virtualRequests));

        return new SortAndVirtualize<TObject, TKey>(source, comparer, virtualRequests).Run();
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
    public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, int size)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");
        }

        return source.Sort(comparer).Top(size);
    }
}
