// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Obsolete: use SortAndBind instead. Sorts using the specified comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort.</param>
    /// <param name="comparer">The <see cref="IComparer{TObject}"/> used to determine sort order.</param>
    /// <param name="sortOptimisations">A <see cref="SortOptimisations"/> that sort optimisation flags. Specify one or more sort optimisations.</param>
    /// <param name="resetThreshold">The number of updates before the entire list is resorted (rather than inline sort).</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// comparer.
    /// </exception>
    /// <seealso cref="ObservableListEx.Sort"/>
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(comparer);

        return new Sort<TObject, TKey>(source, comparer, sortOptimisations, resetThreshold: resetThreshold).Run();
    }

    /// <summary>
    /// Obsolete: use SortAndBind instead. Sorts using a dynamic comparer observable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort.</param>
    /// <param name="comparerObservable">The <see cref="IObservable{IComparer{TObject}}"/> comparer observable.</param>
    /// <param name="sortOptimisations">The <see cref="SortOptimisations"/> sort optimisations.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which emits change sets.</returns>
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IComparer<TObject>> comparerObservable, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(comparerObservable);

        return new Sort<TObject, TKey>(source, null, sortOptimisations, comparerObservable, resetThreshold: resetThreshold).Run();
    }

    /// <summary>
    /// Obsolete: use SortAndBind instead. Sorts using a dynamic comparer observable with a manual re-sort signal.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort.</param>
    /// <param name="comparerObservable">The <see cref="IObservable{IComparer{TObject}}"/> comparer observable.</param>
    /// <param name="resorter">An <see cref="IObservable{Unit}"/> that signals the algorithm to re-sort the entire data set.</param>
    /// <param name="sortOptimisations">The <see cref="SortOptimisations"/> sort optimisations.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which emits change sets.</returns>
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IComparer<TObject>> comparerObservable, IObservable<Unit> resorter, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(comparerObservable);

        return new Sort<TObject, TKey>(source, null, sortOptimisations, comparerObservable, resorter, resetThreshold).Run();
    }

    /// <summary>
    /// Obsolete: use SortAndBind instead. Sorts using a static comparer with a manual re-sort signal.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort.</param>
    /// <param name="comparer">The <see cref="IComparer{TObject}"/> used to determine sort order.</param>
    /// <param name="resorter">An <see cref="IObservable{Unit}"/> that signals the algorithm to re-sort the entire data set.</param>
    /// <param name="sortOptimisations">The <see cref="SortOptimisations"/> sort optimisations.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which emits change sets.</returns>
    [Obsolete(Constants.SortIsObsolete)]
    public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer, IObservable<Unit> resorter, SortOptimisations sortOptimisations = SortOptimisations.None, int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(resorter);

        return new Sort<TObject, TKey>(source, comparer, sortOptimisations, null, resorter, resetThreshold).Run();
    }
}
