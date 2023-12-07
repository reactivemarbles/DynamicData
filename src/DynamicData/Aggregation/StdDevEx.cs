// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Aggregation;

/// <summary>
/// Extensions for calculating standard deviation.
/// </summary>
public static class StdDevEx
{
    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue)
        where T : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue)
        where T : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue)
        where T : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<decimal> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue)
        where T : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0)
        where T : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector, int fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector, long fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector, double fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<decimal> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector, decimal fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector, float fallbackValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().StdDev(valueSelector, fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue = 0) => source.StdDevCalc(t => (long)valueSelector(t), fallbackValue, (current, item) => new StdDev<long>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<long>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue = 0) =>
        source.StdDevCalc(valueSelector, fallbackValue, (current, item) => new StdDev<long>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<long>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<decimal> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue = 0M) =>
        source.StdDevCalc(valueSelector, fallbackValue, (current, item) => new StdDev<decimal>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<decimal>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0M / (values.Count - 1)));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue = 0) => source.StdDevCalc(valueSelector, fallbackValue, (current, item) => new StdDev<double>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<double>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0) => source.StdDevCalc(valueSelector, fallbackValue, (current, item) => new StdDev<float>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<float>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));

    private static IObservable<TResult> StdDevCalc<TObject, TValue, TResult>(this IObservable<IAggregateChangeSet<TObject>> source, Func<TObject, TValue> valueSelector, TResult fallbackValue, Func<StdDev<TValue>, TValue, StdDev<TValue>> addAction, Func<StdDev<TValue>, TValue, StdDev<TValue>> removeAction, Func<StdDev<TValue>, TResult> resultAction)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));
        addAction.ThrowArgumentNullExceptionIfNull(nameof(addAction));
        removeAction.ThrowArgumentNullExceptionIfNull(nameof(removeAction));
        resultAction.ThrowArgumentNullExceptionIfNull(nameof(resultAction));

        return source.Scan(default(StdDev<TValue>), (state, changes) =>
            changes.Aggregate(state, (current, aggregateItem) =>
                aggregateItem.Type == AggregateType.Add ? addAction(current, valueSelector(aggregateItem.Item)) : removeAction(current, valueSelector(aggregateItem.Item)))).Select(values => values.Count < 2 ? fallbackValue : resultAction(values));
    }

    private static decimal Sqrt(decimal x, decimal epsilon = 0.0M)
    {
        if (x < 0)
        {
            throw new OverflowException("Cannot calculate square root from a negative number");
        }

        decimal current = (decimal)Math.Sqrt((double)x), previous;
        do
        {
            previous = current;
            if (previous == 0.0M)
            {
                return 0;
            }

            current = (previous + (x / previous)) / 2;
        }
        while (Math.Abs(previous - current) > epsilon);
        return current;
    }
}
