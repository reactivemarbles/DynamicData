// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Aggregation;

/// <summary>
/// Average extensions.
/// </summary>
public static class AvgEx
{
    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector, int emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int?> valueSelector, int emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector, long emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long?> valueSelector, long emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector, double emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double?> valueSelector, double emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<decimal> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector, decimal emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<decimal> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal?> valueSelector, decimal emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<float> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector, float emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<float> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float?> valueSelector, float emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector, int emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, int?> valueSelector, int emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector, long emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, long?> valueSelector, long emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector, double emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, double?> valueSelector, double emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<decimal> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector, decimal emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<decimal> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal?> valueSelector, decimal emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<float> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector, float emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<float> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, float?> valueSelector, float emptyValue = 0)
        where T : notnull => source.ForAggregation().Avg(valueSelector, emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector, int emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, (current, item) => new Avg<int>(current.Count + 1, current.Sum + item), (current, item) => new Avg<int>(current.Count - 1, current.Sum - item), values => values.Sum / (double)values.Count);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int?> valueSelector, int emptyValue = 0) => source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);

    /// <summary>
    /// Averages the specified value selector.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="emptyValue">The empty value.</param>
    /// <returns>An observable of averages as a double value.</returns>
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector, long emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, (current, item) => new Avg<long>(current.Count + 1, current.Sum + item), (current, item) => new Avg<long>(current.Count - 1, current.Sum - item), values => values.Sum / (double)values.Count);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long?> valueSelector, long emptyValue = 0) => source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector, double emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, (current, item) => new Avg<double>(current.Count + 1, current.Sum + item), (current, item) => new Avg<double>(current.Count - 1, current.Sum - item), values => values.Sum / (double)values.Count);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double?> valueSelector, double emptyValue = 0) => source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<decimal> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector, decimal emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, (current, item) => new Avg<decimal>(current.Count + 1, current.Sum + item), (current, item) => new Avg<decimal>(current.Count - 1, current.Sum - item), values => values.Sum / values.Count);

    /// <summary>
    /// Averages the specified value selector.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <param name="emptyValue">The empty value.</param>
    /// <returns>An observable of decimals with the averaged values.</returns>
    public static IObservable<decimal> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal?> valueSelector, decimal emptyValue = 0) => source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<float> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector, float emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, (current, item) => new Avg<float>(current.Count + 1, current.Sum + item), (current, item) => new Avg<float>(current.Count - 1, current.Sum - item), values => values.Sum / values.Count);

    /// <summary>
    /// Continuous calculation of the average of the underlying data source.
    /// </summary>
    /// <typeparam name="T">The type to average.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="valueSelector">The function which returns the value.</param>
    /// <param name="emptyValue">The resulting average value when there is no data.</param>
    /// <returns>
    /// An observable of averages.
    /// </returns>
    public static IObservable<float> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float?> valueSelector, float emptyValue = 0) => source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);

    private static IObservable<TResult> AvgCalc<TObject, TValue, TResult>(this IObservable<IAggregateChangeSet<TObject>> source, Func<TObject, TValue> valueSelector, TResult fallbackValue, Func<Avg<TValue>, TValue, Avg<TValue>> addAction, Func<Avg<TValue>, TValue, Avg<TValue>> removeAction, Func<Avg<TValue>, TResult> resultAction)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));
        addAction.ThrowArgumentNullExceptionIfNull(nameof(addAction));
        removeAction.ThrowArgumentNullExceptionIfNull(nameof(removeAction));
        resultAction.ThrowArgumentNullExceptionIfNull(nameof(resultAction));

        return source.Scan(default(Avg<TValue>), (state, changes) =>
            changes.Aggregate(state, (current, aggregateItem) =>
                aggregateItem.Type == AggregateType.Add ? addAction(current, valueSelector(aggregateItem.Item)) : removeAction(current, valueSelector(aggregateItem.Item)))).Select(values => values.Count == 0 ? fallbackValue : resultAction(values));
    }
}
