// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// Extensions for calculating sample standard deviation.
/// </summary>
/// <remarks>
/// When fewer than two items are present, the configured fallback value is returned.
/// </remarks>
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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue)
        where T : notnull => source.StdDev(Widen(valueSelector), fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<long, StdDev<long>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<double, StdDev<double>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<decimal> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<decimal, StdDev<decimal>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<float, StdDev<float>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector, int fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.StdDev(Widen(valueSelector), fallbackValue);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector, long fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<long, StdDev<long>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector, double fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<double, StdDev<double>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<decimal> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector, decimal fallbackValue)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<decimal, StdDev<decimal>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().StdDev(...)</c> instead computes the deviation without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector, float fallbackValue = 0)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<float, StdDev<float>>(default, Include, Exclude), accumulator => accumulator.State.Count < 2 ? fallbackValue : Deviation(accumulator.State));

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue = 0) =>
        source.StdDevCalc(Widen(valueSelector), fallbackValue, Include, Exclude, Deviation);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue = 0) =>
        source.StdDevCalc(valueSelector, fallbackValue, Include, Exclude, Deviation);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<decimal> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue = 0M) =>
        source.StdDevCalc(valueSelector, fallbackValue, Include, Exclude, Deviation);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue = 0) =>
        source.StdDevCalc(valueSelector, fallbackValue, Include, Exclude, Deviation);

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0) =>
        source.StdDevCalc(valueSelector, fallbackValue, Include, Exclude, Deviation);

    private static IObservable<TResult> StdDevCalc<TObject, TValue, TResult>(
        this IObservable<IAggregateChangeSet<TObject>> source,
        Func<TObject, TValue> valueSelector,
        TResult fallbackValue,
        Func<StdDev<TValue>, TValue, StdDev<TValue>> addAction,
        Func<StdDev<TValue>, TValue, StdDev<TValue>> removeAction,
        Func<StdDev<TValue>, TResult> resultAction)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(addAction);
        ArgumentNullException.ThrowIfNull(removeAction);
        ArgumentNullException.ThrowIfNull(resultAction);

        return source
            .Scan(
                default(StdDev<TValue>),
                (state, changes) => changes.Aggregate(
                    state,
                    (current, aggregateItem) => aggregateItem.Type == AggregateType.Add
                        ? addAction(current, valueSelector(aggregateItem.Item))
                        : removeAction(current, valueSelector(aggregateItem.Item))))
            .Select(values => values.Count < 2 ? fallbackValue : resultAction(values));
    }

    private static Func<T, long> Widen<T>(Func<T, int> valueSelector)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        return item => valueSelector(item);
    }

    private static StdDev<long> Include(StdDev<long> current, long value) => new(current.Count + 1, current.SumOfItems + value, current.SumOfSquares + (value * value));

    private static StdDev<double> Include(StdDev<double> current, double value) => new(current.Count + 1, current.SumOfItems + value, current.SumOfSquares + (value * value));

    private static StdDev<decimal> Include(StdDev<decimal> current, decimal value) => new(current.Count + 1, current.SumOfItems + value, current.SumOfSquares + (value * value));

    private static StdDev<float> Include(StdDev<float> current, float value) => new(current.Count + 1, current.SumOfItems + value, current.SumOfSquares + (value * value));

    private static StdDev<long> Exclude(StdDev<long> current, long value) => new(current.Count - 1, current.SumOfItems - value, current.SumOfSquares - (value * value));

    private static StdDev<double> Exclude(StdDev<double> current, double value) => new(current.Count - 1, current.SumOfItems - value, current.SumOfSquares - (value * value));

    private static StdDev<decimal> Exclude(StdDev<decimal> current, decimal value) => new(current.Count - 1, current.SumOfItems - value, current.SumOfSquares - (value * value));

    private static StdDev<float> Exclude(StdDev<float> current, float value) => new(current.Count - 1, current.SumOfItems - value, current.SumOfSquares - (value * value));

    private static double Deviation(StdDev<double> values) => Math.Sqrt((values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) / (values.Count - 1));

    private static double Deviation(StdDev<float> values) => Math.Sqrt((values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) / (values.Count - 1));

    private static decimal Deviation(StdDev<decimal> values) => Sqrt((values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) / (values.Count - 1));

    private static double Deviation(StdDev<long> values)
    {
        // Cross-multiplying keeps the moment subtraction in integer arithmetic. Converting the moments to double first
        // collapses the difference to zero once they exceed the exactly representable integer range, and dividing before
        // subtracting truncates the mean. The value below is the sum of squared deviations scaled by Count, which the
        // matching Count factor in the divisor removes. Decimal carries the products because the long accumulators can
        // exceed the exact-integer range of double, and by Cauchy-Schwarz both products stay within decimal range
        // whenever the accumulators themselves have not overflowed. The numerator is therefore exact under that
        // condition, while the division and square root below still round.
        var count = (decimal)values.Count;
        var sumOfItems = (decimal)values.SumOfItems;
        var scaledSumOfSquaredDeviations = ((decimal)values.SumOfSquares * count) - (sumOfItems * sumOfItems);
        return Math.Sqrt((double)(scaledSumOfSquaredDeviations / (count * (count - 1))));
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
