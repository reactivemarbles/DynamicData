// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Aggregation;

/// <summary>
/// Provides immutable-item sum aggregation extensions.
/// </summary>
public static partial class SumEx
{
    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutableNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutableNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutableNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutableNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> SumImmutable<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float?> valueSelector)
        where TObject : notnull
        where TKey : notnull => SumCacheImmutableNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector)
        where T : notnull => SumListImmutable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, int?> valueSelector)
        where T : notnull => SumListImmutableNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector)
        where T : notnull => SumListImmutable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, long?> valueSelector)
        where T : notnull => SumListImmutableNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector)
        where T : notnull => SumListImmutable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, double?> valueSelector)
        where T : notnull => SumListImmutableNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector)
        where T : notnull => SumListImmutable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal?> valueSelector)
        where T : notnull => SumListImmutableNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector)
        where T : notnull => SumListImmutable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continually computes the sum, optimized for immutable items. Refresh changes do not re-evaluate items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> SumImmutable<T>(this IObservable<IChangeSet<T>> source, Func<T, float?> valueSelector)
        where T : notnull => SumListImmutableNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    private static IObservable<TValue> SumCacheImmutable<TObject, TKey, TValue>(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TValue> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Scan(seed, (sum, changes) =>
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        sum = add(sum, valueSelector(change.Current));
                        break;

                    case ChangeReason.Update:
                        sum = subtract(sum, valueSelector(change.Previous.Value));
                        sum = add(sum, valueSelector(change.Current));
                        break;

                    case ChangeReason.Remove:
                        sum = subtract(sum, valueSelector(change.Current));
                        break;
                }
            }

            return sum;
        });
    }

    private static IObservable<TValue> SumCacheImmutableNullable<TObject, TKey, TValue>(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TValue?> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
        where TKey : notnull
        where TValue : struct
    {
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return SumCacheImmutable(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }

    private static IObservable<TValue> SumListImmutable<TObject, TValue>(
        IObservable<IChangeSet<TObject>> source,
        Func<TObject, TValue> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Scan(seed, (sum, changes) =>
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        sum = add(sum, valueSelector(change.Item.Current));
                        break;

                    case ListChangeReason.AddRange:
                        foreach (var item in change.Range)
                        {
                            sum = add(sum, valueSelector(item));
                        }

                        break;

                    case ListChangeReason.Replace:
                        sum = subtract(sum, valueSelector(change.Item.Previous.Value));
                        sum = add(sum, valueSelector(change.Item.Current));
                        break;

                    case ListChangeReason.Remove:
                        sum = subtract(sum, valueSelector(change.Item.Current));
                        break;

                    case ListChangeReason.RemoveRange:
                    case ListChangeReason.Clear:
                        foreach (var item in change.Range)
                        {
                            sum = subtract(sum, valueSelector(item));
                        }

                        break;
                }
            }

            return sum;
        });
    }

    private static IObservable<TValue> SumListImmutableNullable<TObject, TValue>(
        IObservable<IChangeSet<TObject>> source,
        Func<TObject, TValue?> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
        where TValue : struct
    {
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return SumListImmutable(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }
}
