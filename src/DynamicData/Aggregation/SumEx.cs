// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Aggregation;

/// <summary>
/// Aggregation extensions.
/// </summary>
/// <remarks>
/// Sum overloads operating directly on cache and list change sets retain projection state and re-evaluate refreshed items.
/// </remarks>
public static partial class SumEx
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
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStateful(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

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
        where TKey : notnull => SumCacheStatefulNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, int?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, long?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, double?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector)
        where T : notnull => SumListStateful(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IChangeSet<T>> source, Func<T, float?> valueSelector)
        where T : notnull => SumListStatefulNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector)
        => SumAggregate(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<int> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector)
        => SumAggregate(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<long> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0L, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector)
        => SumAggregate(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<double> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0D, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector)
        => SumAggregate(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<decimal> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0M, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector)
        => SumAggregate(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    /// <summary>
    /// Continual computes the sum of values matching the value selector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>An observable which emits the summed value.</returns>
    public static IObservable<float> Sum<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float?> valueSelector)
        => SumAggregateNullable(source, valueSelector, 0F, static (current, value) => current + value, static (current, value) => current - value);

    private static IObservable<TValue> SumCacheStateful<TObject, TKey, TValue>(
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

        return Observable.Defer(() =>
        {
            var values = new Dictionary<TKey, TValue>();

            return source.Scan(seed, (sum, changes) =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                var value = valueSelector(change.Current);
                                values.Add(change.Key, value);
                                sum = add(sum, value);
                                break;
                            }

                        case ChangeReason.Update:
                        case ChangeReason.Refresh:
                            {
                                var previousValue = values[change.Key];
                                var currentValue = valueSelector(change.Current);
                                values[change.Key] = currentValue;
                                sum = subtract(sum, previousValue);
                                sum = add(sum, currentValue);
                                break;
                            }

                        case ChangeReason.Remove:
                            {
                                var value = values[change.Key];
                                values.Remove(change.Key);
                                sum = subtract(sum, value);
                                break;
                            }
                    }
                }

                return sum;
            });
        });
    }

    private static IObservable<TValue> SumCacheStatefulNullable<TObject, TKey, TValue>(
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

        return SumCacheStateful(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }

    private static IObservable<TValue> SumListStateful<TObject, TValue>(
        IObservable<IChangeSet<TObject>> source,
        Func<TObject, TValue> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return Observable.Defer(() =>
        {
            var values = new List<(TObject Item, TValue Value)>();

            return source.Scan(seed, (sum, changes) =>
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            {
                                var item = (Item: change.Item.Current, Value: valueSelector(change.Item.Current));
                                if (change.Item.CurrentIndex < 0 || change.Item.CurrentIndex >= values.Count)
                                {
                                    values.Add(item);
                                }
                                else
                                {
                                    values.Insert(change.Item.CurrentIndex, item);
                                }

                                sum = add(sum, item.Value);
                                break;
                            }

                        case ListChangeReason.AddRange:
                            {
                                var items = new List<(TObject Item, TValue Value)>(change.Range.Count);
                                foreach (var item in change.Range)
                                {
                                    var value = valueSelector(item);
                                    items.Add((item, value));
                                    sum = add(sum, value);
                                }

                                if (change.Range.Index < 0 || change.Range.Index >= values.Count)
                                {
                                    values.AddRange(items);
                                }
                                else
                                {
                                    values.InsertRange(change.Range.Index, items);
                                }

                                break;
                            }

                        case ListChangeReason.Replace:
                            {
                                var previousIndex = change.Item.PreviousIndex;
                                if (previousIndex < 0)
                                {
                                    previousIndex = IndexOf(values, change.Item.Previous.Value);
                                }

                                if (previousIndex < 0)
                                {
                                    throw new UnspecifiedIndexException($"Cannot find index of {change.Item.Previous.Value}");
                                }

                                var previousValue = values[previousIndex].Value;
                                var currentValue = valueSelector(change.Item.Current);

                                if (change.Item.CurrentIndex < 0 || change.Item.CurrentIndex == previousIndex)
                                {
                                    values[previousIndex] = (change.Item.Current, currentValue);
                                }
                                else
                                {
                                    values.RemoveAt(previousIndex);
                                    values.Insert(change.Item.CurrentIndex, (change.Item.Current, currentValue));
                                }

                                sum = subtract(sum, previousValue);
                                sum = add(sum, currentValue);
                                break;
                            }

                        case ListChangeReason.Remove:
                            {
                                var index = change.Item.CurrentIndex;
                                if (index < 0)
                                {
                                    index = IndexOf(values, change.Item.Current);
                                }

                                if (index < 0)
                                {
                                    throw new UnspecifiedIndexException($"Cannot find index of {change.Item.Current}");
                                }

                                var value = values[index].Value;
                                values.RemoveAt(index);
                                sum = subtract(sum, value);
                                break;
                            }

                        case ListChangeReason.RemoveRange:
                            if (change.Range.Index >= 0)
                            {
                                var rangeEnd = change.Range.Index + change.Range.Count;
                                for (var index = change.Range.Index; index < rangeEnd; ++index)
                                {
                                    sum = subtract(sum, values[index].Value);
                                }

                                values.RemoveRange(change.Range.Index, change.Range.Count);
                            }
                            else
                            {
                                foreach (var item in change.Range)
                                {
                                    var index = IndexOf(values, item);
                                    if (index < 0)
                                    {
                                        throw new UnspecifiedIndexException($"Cannot find index of {item}");
                                    }

                                    sum = subtract(sum, values[index].Value);
                                    values.RemoveAt(index);
                                }
                            }

                            break;

                        case ListChangeReason.Clear:
                            foreach (var item in values)
                            {
                                sum = subtract(sum, item.Value);
                            }

                            values.Clear();
                            break;

                        case ListChangeReason.Moved:
                            {
                                var item = values[change.Item.PreviousIndex];
                                values.RemoveAt(change.Item.PreviousIndex);
                                values.Insert(change.Item.CurrentIndex, item);
                                break;
                            }

                        case ListChangeReason.Refresh:
                            {
                                var index = change.Item.CurrentIndex;
                                if (index < 0)
                                {
                                    index = IndexOf(values, change.Item.Current);
                                }

                                if (index < 0)
                                {
                                    throw new UnspecifiedIndexException($"Cannot find index of {change.Item.Current}");
                                }

                                var previousValue = values[index].Value;
                                var currentValue = valueSelector(change.Item.Current);
                                values[index] = (change.Item.Current, currentValue);
                                sum = subtract(sum, previousValue);
                                sum = add(sum, currentValue);
                                break;
                            }
                    }
                }

                return sum;
            });
        });
    }

    private static IObservable<TValue> SumListStatefulNullable<TObject, TValue>(
        IObservable<IChangeSet<TObject>> source,
        Func<TObject, TValue?> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TObject : notnull
        where TValue : struct
    {
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return SumListStateful(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }

    private static int IndexOf<TObject, TValue>(List<(TObject Item, TValue Value)> values, TObject item)
        where TObject : notnull
    {
        for (var index = 0; index < values.Count; ++index)
        {
            if (ReferenceEquals(values[index].Item, item))
            {
                return index;
            }
        }

        var comparer = EqualityComparer<TObject>.Default;
        for (var index = 0; index < values.Count; ++index)
        {
            if (comparer.Equals(values[index].Item, item))
            {
                return index;
            }
        }

        return -1;
    }

    private static IObservable<TValue> SumAggregate<TObject, TValue>(
        IObservable<IAggregateChangeSet<TObject>> source,
        Func<TObject, TValue> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.Scan(seed, (sum, changes) =>
        {
            foreach (var change in changes)
            {
                var value = valueSelector(change.Item);
                sum = change.Type == AggregateType.Add ? add(sum, value) : subtract(sum, value);
            }

            return sum;
        });
    }

    private static IObservable<TValue> SumAggregateNullable<TObject, TValue>(
        IObservable<IAggregateChangeSet<TObject>> source,
        Func<TObject, TValue?> valueSelector,
        TValue seed,
        Func<TValue, TValue, TValue> add,
        Func<TValue, TValue, TValue> subtract)
        where TValue : struct
    {
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return SumAggregate(source, item => valueSelector(item).GetValueOrDefault(), seed, add, subtract);
    }
}
