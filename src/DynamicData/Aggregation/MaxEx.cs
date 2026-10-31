// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// Maximum and minimum value extensions.
/// </summary>
public static class MaxEx
{
    /// <summary>
    /// Continually calculates the maximum value from the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="emptyValue">The value to use when the underlying collection is empty.</param>
    /// <returns>
    /// A distinct observable of the maximum item.
    /// </returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored. When the item holding the maximum is removed, or
    /// is updated or refreshed to a lower value, the maximum is recomputed from the stored values, so memory grows with
    /// the number of items.
    /// </remarks>
    public static IObservable<TResult> Maximum<TObject, TResult>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TResult> valueSelector, TResult emptyValue = default)
        where TObject : notnull
        where TResult : struct, IComparable<TResult> => source.AggregateProjections(valueSelector, ExtremeAccumulator<TResult>.CreateMaximum(), accumulator => accumulator.Extreme ?? emptyValue).DistinctUntilChanged();

    /// <summary>
    /// Continually calculates the maximum value from the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="emptyValue">The value to use when the underlying collection is empty.</param>
    /// <returns>
    /// A distinct observable of the maximum item.
    /// </returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored. When the item holding the maximum is removed, or
    /// is updated or refreshed to a lower value, the maximum is recomputed from the stored values, so memory grows with
    /// the number of items.
    /// </remarks>
    public static IObservable<TResult> Maximum<TObject, TKey, TResult>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TResult> valueSelector, TResult emptyValue = default)
        where TObject : notnull
        where TKey : notnull
        where TResult : struct, IComparable<TResult> => source.AggregateProjections(valueSelector, ExtremeAccumulator<TResult>.CreateMaximum(), accumulator => accumulator.Extreme ?? emptyValue).DistinctUntilChanged();

    /// <summary>
    /// Continually calculates the minimum value from the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="emptyValue">The value to use when the underlying collection is empty.</param>
    /// <returns>A distinct observable of the minimums item.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored. When the item holding the minimum is removed, or
    /// is updated or refreshed to a higher value, the minimum is recomputed from the stored values, so memory grows with
    /// the number of items.
    /// </remarks>
    public static IObservable<TResult> Minimum<TObject, TResult>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TResult> valueSelector, TResult emptyValue = default)
        where TObject : notnull
        where TResult : struct, IComparable<TResult> => source.AggregateProjections(valueSelector, ExtremeAccumulator<TResult>.CreateMinimum(), accumulator => accumulator.Extreme ?? emptyValue).DistinctUntilChanged();

    /// <summary>
    /// Continually calculates the minimum value from the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="emptyValue">The value to use when the underlying collection is empty.</param>
    /// <returns>
    /// A distinct observable of the minimums item.
    /// </returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored. When the item holding the minimum is removed, or
    /// is updated or refreshed to a higher value, the minimum is recomputed from the stored values, so memory grows with
    /// the number of items.
    /// </remarks>
    public static IObservable<TResult> Minimum<TObject, TKey, TResult>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TResult> valueSelector, TResult emptyValue = default)
        where TObject : notnull
        where TKey : notnull
        where TResult : struct, IComparable<TResult> => source.AggregateProjections(valueSelector, ExtremeAccumulator<TResult>.CreateMinimum(), accumulator => accumulator.Extreme ?? emptyValue).DistinctUntilChanged();
}
