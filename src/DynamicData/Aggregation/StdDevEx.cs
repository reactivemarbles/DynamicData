// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Aggregation;
#else

namespace DynamicData.Aggregation;
#endif

/// <summary>
/// Extensions for calculating standard deviation.
/// </summary>
/// <remarks>
/// Computes sample standard deviation using incremental central moments. Integral and decimal
/// selectors use decimal accumulators; their central moments must fit in <see cref="decimal"/>.
/// Floating-point selectors retain the precision limits of double arithmetic.
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
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<decimal> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

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
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

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
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

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
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

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
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

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
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.ForAggregation().StdDev(valueSelector, fallbackValue);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue = 0)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);
        return source.StdDevCalc(t => (decimal)valueSelector(t), (decimal)fallbackValue).Select(value => (double)value);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue = 0)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);
        return source.StdDevCalc(t => (decimal)valueSelector(t), (decimal)fallbackValue).Select(value => (double)value);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<decimal> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue = 0M)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);
        return source.StdDevCalc(valueSelector, fallbackValue);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue = 0)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);
        return source.StdDevCalc(valueSelector, fallbackValue);
    }

    /// <summary>
    /// Continual computation of the standard deviation of the  values in the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="fallbackValue">The fallback value.</param>
    /// <returns>An observable which emits the standard deviation value.</returns>
    public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);
        return source.StdDevCalc(t => valueSelector(t), fallbackValue);
    }

    /// <summary>
    /// Executes the StdDevCalc operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="valueSelector">The valueSelector value.</param>
    /// <param name="fallbackValue">The fallbackValue value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<double> StdDevCalc<TObject>(this IObservable<IAggregateChangeSet<TObject>> source, Func<TObject, double> valueSelector, double fallbackValue)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.Scan(
            default(StdDev<double>),
            (state, changes) =>
            {
                foreach (var aggregateItem in changes)
                {
                    state = aggregateItem.Type == AggregateType.Add ? Add(state, valueSelector(aggregateItem.Item)) : Remove(state, valueSelector(aggregateItem.Item));
                }

                return state;
            }).Select(values => values.Count < 2 ? fallbackValue : Math.Sqrt(Math.Max(values.SumOfSquaredDifferences, 0D) / (values.Count - 1)));
    }

    /// <summary>
    /// Executes the StdDevCalc operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="valueSelector">The valueSelector value.</param>
    /// <param name="fallbackValue">The fallbackValue value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<decimal> StdDevCalc<TObject>(this IObservable<IAggregateChangeSet<TObject>> source, Func<TObject, decimal> valueSelector, decimal fallbackValue)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return source.Scan(
            default(StdDev<decimal>),
            (state, changes) =>
            {
                foreach (var aggregateItem in changes)
                {
                    state = aggregateItem.Type == AggregateType.Add ? Add(state, valueSelector(aggregateItem.Item)) : Remove(state, valueSelector(aggregateItem.Item));
                }

                return state;
            }).Select(values => values.Count < 2 ? fallbackValue : Sqrt(Math.Max(values.SumOfSquaredDifferences, 0M) / (values.Count - 1)));
    }

    private static StdDev<double> Add(StdDev<double> current, double item)
    {
        var count = current.Count + 1;
        var delta = item - current.Mean;
        var mean = current.Mean + (delta / count);
        var delta2 = item - mean;
        return new StdDev<double>(count, mean, current.SumOfSquaredDifferences + (delta * delta2));
    }

    private static StdDev<double> Remove(StdDev<double> current, double item)
    {
        if (current.Count <= 1)
        {
            return default;
        }

        var count = current.Count - 1;
        var delta = item - current.Mean;
        var mean = current.Mean - (delta / count);
        if (count == 1)
        {
            return new StdDev<double>(count, mean, 0D);
        }

        var delta2 = item - mean;
        return new StdDev<double>(count, mean, current.SumOfSquaredDifferences - (delta * delta2));
    }

    private static StdDev<decimal> Add(StdDev<decimal> current, decimal item)
    {
        var count = current.Count + 1;
        var delta = item - current.Mean;
        var mean = current.Mean + (delta / count);
        var delta2 = item - mean;
        return new StdDev<decimal>(count, mean, current.SumOfSquaredDifferences + (delta * delta2));
    }

    private static StdDev<decimal> Remove(StdDev<decimal> current, decimal item)
    {
        if (current.Count <= 1)
        {
            return default;
        }

        var count = current.Count - 1;
        var delta = item - current.Mean;
        var mean = current.Mean - (delta / count);
        if (count == 1)
        {
            return new StdDev<decimal>(count, mean, 0M);
        }

        var delta2 = item - mean;
        return new StdDev<decimal>(count, mean, current.SumOfSquaredDifferences - (delta * delta2));
    }

    /// <summary>
    /// Executes the Sqrt operation.
    /// </summary>
    /// <param name="x">The x value.</param>
    /// <returns>The result of the operation.</returns>
    private static decimal Sqrt(decimal x)
    {
        if (x < 0)
        {
            throw new OverflowException("Cannot calculate square root from a negative number");
        }

        var current = (decimal)Math.Sqrt((double)x);
        var previous = 0M;
        while (current != 0M)
        {
            var next = (current + (x / current)) / 2;
            // Decimal rounding can alternate between two adjacent values.
            if (next == current || next == previous)
            {
                return next;
            }

            previous = current;
            current = next;
        }

        return current;
    }
}
