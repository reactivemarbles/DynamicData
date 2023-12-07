// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Aggregation;

/// <summary>
/// Aggregation extensions.
/// </summary>
public static class SumEx
{
    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int?> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long?> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double?> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal?> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float?> valueSelector)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, int?> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, long?> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, double?> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal?> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, float?> valueSelector)
        where T : notnull => source.ForAggregation().Sum(valueSelector);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0, valueSelector, (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int?> valueSelector) => source.Accumulate(0, t => valueSelector(t).GetValueOrDefault(), (current, value) => current + value, (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0, valueSelector, (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long?> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0L, t => valueSelector(t).ValueOr(0), (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0, valueSelector, (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double?> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0D, t => valueSelector(t).ValueOr(0), (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0, valueSelector, (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal?> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0M, t => valueSelector(t).ValueOr(0), (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0, valueSelector, (current, value) => current + value, (current, value) => current - value);
    }

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float?> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Accumulate(0F, t => valueSelector(t).ValueOr(0), (current, value) => current + value, (current, value) => current - value);
    }
}
