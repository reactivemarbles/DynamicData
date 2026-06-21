// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
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
    /// Sorts the changeset stream by the value returned from <paramref name="expression"/>. Creates a comparer internally
    /// and delegates to <see cref="Sort{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IComparer{TObject}, SortOptimisations, int)"/>.
    /// Since Sort is obsolete, prefer SortAndBind for new code.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort.</param>
    /// <param name="expression">A <see cref="Func{T, TResult}"/> that expression that selects a comparable value from each item.</param>
    /// <param name="sortOrder">The <see cref="SortDirection"/> sort direction. Defaults to ascending.</param>
    /// <param name="sortOptimisations">A <see cref="SortOptimisations"/> that sort optimization flags.</param>
    /// <param name="resetThreshold">The number of updates before the entire list is re-sorted (rather than inline sort).</param>
    /// <returns>An observable that emits sorted changesets.</returns>
    public static IObservable<ISortedChangeSet<TObject, TKey>> SortBy<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, IComparable> expression,
        SortDirection sortOrder = SortDirection.Ascending,
        SortOptimisations sortOptimisations = SortOptimisations.None,
        int resetThreshold = DefaultSortResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        source = source ?? throw new ArgumentNullException(nameof(source));
        expression = expression ?? throw new ArgumentNullException(nameof(expression));

        return source.Sort(
            sortOrder switch
            {
                SortDirection.Descending => SortExpressionComparer<TObject>.Descending(expression),
                _ => SortExpressionComparer<TObject>.Ascending(expression),
            },
            sortOptimisations,
            resetThreshold);
    }
}
