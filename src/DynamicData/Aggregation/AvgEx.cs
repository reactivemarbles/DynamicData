// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector, int emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<int, Avg<int>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int?> valueSelector, int emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector, long emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<long, Avg<long>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long?> valueSelector, long emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector, double emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<double, Avg<double>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double?> valueSelector, double emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<decimal> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector, decimal emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<decimal, Avg<decimal>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<decimal> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal?> valueSelector, decimal emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<float> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector, float emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<float, Avg<float>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<float> Avg<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float?> valueSelector, float emptyValue = 0)
        where TObject : notnull
        where TKey : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector, int emptyValue = 0)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<int, Avg<int>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, int?> valueSelector, int emptyValue = 0)
        where T : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector, long emptyValue = 0)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<long, Avg<long>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, long?> valueSelector, long emptyValue = 0)
        where T : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector, double emptyValue = 0)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<double, Avg<double>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<double> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, double?> valueSelector, double emptyValue = 0)
        where T : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<decimal> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector, decimal emptyValue = 0)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<decimal, Avg<decimal>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<decimal> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal?> valueSelector, decimal emptyValue = 0)
        where T : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<float> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector, float emptyValue = 0)
        where T : notnull => source.AggregateProjections(valueSelector, new InvertibleAccumulator<float, Avg<float>>(default, Include, Exclude), accumulator => accumulator.State.Count == 0 ? emptyValue : Mean(accumulator.State));

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
    /// <remarks>
    /// A refresh re-evaluates <paramref name="valueSelector"/> for the refreshed item, so the result follows items that
    /// change in place. The value last selected for each item is stored, and updates and removals exclude that stored value
    /// rather than re-evaluating the previous item, so memory grows with the number of items.
    /// Calling <c>ForAggregation().Avg(...)</c> instead computes the average without that state, but ignores refreshes.
    /// </remarks>
    public static IObservable<float> Avg<T>(this IObservable<IChangeSet<T>> source, Func<T, float?> valueSelector, float emptyValue = 0)
        where T : notnull => source.Avg(ValueOrDefault(valueSelector), emptyValue);

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
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector, int emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, Include, Exclude, Mean);

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
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector, long emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, Include, Exclude, Mean);

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
    public static IObservable<double> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector, double emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, Include, Exclude, Mean);

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
    public static IObservable<decimal> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector, decimal emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, Include, Exclude, Mean);

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
    public static IObservable<float> Avg<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector, float emptyValue = 0) => source.AvgCalc(valueSelector, emptyValue, Include, Exclude, Mean);

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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(addAction);
        ArgumentNullException.ThrowIfNull(removeAction);
        ArgumentNullException.ThrowIfNull(resultAction);

        return source.Scan(default(Avg<TValue>), (state, changes) =>
            changes.Aggregate(state, (current, aggregateItem) =>
                aggregateItem.Type == AggregateType.Add ? addAction(current, valueSelector(aggregateItem.Item)) : removeAction(current, valueSelector(aggregateItem.Item)))).Select(values => values.Count == 0 ? fallbackValue : resultAction(values));
    }

    private static Func<T, TValue> ValueOrDefault<T, TValue>(Func<T, TValue?> valueSelector)
        where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        return item => valueSelector(item).GetValueOrDefault();
    }

    private static Avg<int> Include(Avg<int> current, int value) => new(current.Count + 1, current.Sum + value);

    private static Avg<long> Include(Avg<long> current, long value) => new(current.Count + 1, current.Sum + value);

    private static Avg<double> Include(Avg<double> current, double value) => new(current.Count + 1, current.Sum + value);

    private static Avg<decimal> Include(Avg<decimal> current, decimal value) => new(current.Count + 1, current.Sum + value);

    private static Avg<float> Include(Avg<float> current, float value) => new(current.Count + 1, current.Sum + value);

    private static Avg<int> Exclude(Avg<int> current, int value) => new(current.Count - 1, current.Sum - value);

    private static Avg<long> Exclude(Avg<long> current, long value) => new(current.Count - 1, current.Sum - value);

    private static Avg<double> Exclude(Avg<double> current, double value) => new(current.Count - 1, current.Sum - value);

    private static Avg<decimal> Exclude(Avg<decimal> current, decimal value) => new(current.Count - 1, current.Sum - value);

    private static Avg<float> Exclude(Avg<float> current, float value) => new(current.Count - 1, current.Sum - value);

    private static double Mean(Avg<int> values) => values.Sum / (double)values.Count;

    private static double Mean(Avg<long> values) => values.Sum / (double)values.Count;

    private static double Mean(Avg<double> values) => values.Sum / values.Count;

    private static decimal Mean(Avg<decimal> values) => values.Sum / values.Count;

    private static float Mean(Avg<float> values) => values.Sum / values.Count;
}
