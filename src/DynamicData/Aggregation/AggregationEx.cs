// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// Aggregation extensions.
/// </summary>
public static class AggregationEx
{
    /// <summary>
    /// Transforms the change set into an enumerable which is suitable for high performing aggregations.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The aggregated change set.</returns>
    public static IObservable<IAggregateChangeSet<TObject>> ForAggregation<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Select(changeSet => (IAggregateChangeSet<TObject>)new AggregateEnumerator<TObject, TKey>(changeSet));
    }

    /// <summary>
    /// Transforms the change set into an enumerable which is suitable for high performing aggregations.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The aggregated change set.</returns>
    public static IObservable<IAggregateChangeSet<TObject>> ForAggregation<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Select(changeSet => (IAggregateChangeSet<TObject>)new AggregateEnumerator<TObject>(changeSet));
    }

    /// <summary>
    /// Used to invalidate an aggregating stream. Used when there has been an inline change
    /// i.e. a property changed or meta data has changed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="invalidate">The invalidate.</param>
    /// <returns>An observable which emits the value.</returns>
    public static IObservable<T> InvalidateWhen<T>(this IObservable<T> source, IObservable<Unit> invalidate) =>
        invalidate.StartWith(Unit.Default).Select(_ => source).Switch().DistinctUntilChanged();

    /// <summary>
    /// Used to invalidate an aggregating stream. Used when there has been an inline change.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <typeparam name="TTrigger">The type of the trigger.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="invalidate">The invalidate.</param>
    /// <returns>An observable which emits the value.</returns>
    public static IObservable<T> InvalidateWhen<T, TTrigger>(this IObservable<T> source, IObservable<TTrigger?> invalidate) =>
        invalidate.StartWith(default(TTrigger)).Select(_ => source).Switch().DistinctUntilChanged();

    /// <summary>
    /// Aggregates the projected value of every item in a cache, re-evaluating the projection when an item is refreshed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the projected value.</typeparam>
    /// <typeparam name="TAccumulator">The type of the accumulator.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">Projects an item to the value being aggregated.</param>
    /// <param name="accumulator">The accumulator in its empty state. Each subscription works on its own copy.</param>
    /// <param name="resultSelector">Reads the result from the accumulator after each change set.</param>
    /// <returns>An observable which emits the result once per change set.</returns>
    /// <remarks>
    /// The last projection of each key is stored, so updates and removals exclude the value that was included rather than
    /// re-evaluating the previous item. A change set that adds an existing key, or updates, refreshes or removes an unknown
    /// key, faults the stream.
    /// </remarks>
    internal static IObservable<TResult> AggregateProjections<TObject, TKey, TValue, TAccumulator, TResult>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector, TAccumulator accumulator, Func<TAccumulator, TResult> resultSelector)
        where TObject : notnull
        where TKey : notnull
        where TAccumulator : struct, IAccumulator<TValue>
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        return Observable.Defer(() =>
        {
            var state = accumulator;
            var values = new Dictionary<TKey, TValue>();

            return source.Select(changes =>
            {
                var isValid = true;

                foreach (var change in changes.ToConcreteType())
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                var value = valueSelector(change.Current);
                                values.Add(change.Key, value);
                                state.Add(value);
                                break;
                            }

                        case ChangeReason.Update:
                        case ChangeReason.Refresh:
                            {
                                var previous = values[change.Key];
                                var current = valueSelector(change.Current);
                                values[change.Key] = current;
                                isValid &= state.Replace(previous, current);
                                break;
                            }

                        case ChangeReason.Remove:
                            {
                                var value = values[change.Key];
                                values.Remove(change.Key);
                                isValid &= state.Remove(value);
                                break;
                            }
                    }
                }

                if (!isValid)
                {
                    state.Reset();
                    foreach (var value in values.Values)
                    {
                        state.Add(value);
                    }
                }

                return resultSelector(state);
            });
        });
    }

    /// <summary>
    /// Aggregates the projected value of every item in a list, re-evaluating the projection when an item is refreshed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TValue">The type of the projected value.</typeparam>
    /// <typeparam name="TAccumulator">The type of the accumulator.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">Projects an item to the value being aggregated.</param>
    /// <param name="accumulator">The accumulator in its empty state. Each subscription works on its own copy.</param>
    /// <param name="resultSelector">Reads the result from the accumulator after each change set.</param>
    /// <returns>An observable which emits the result once per change set.</returns>
    /// <remarks>
    /// The last projection of each item is stored in list order, so replacements and removals exclude the value that was
    /// included rather than re-evaluating the previous item. Changes without an index are matched by reference first and then
    /// by the default equality comparer. A removal or refresh of an item that is not in the list faults the stream.
    /// </remarks>
    internal static IObservable<TResult> AggregateProjections<TObject, TValue, TAccumulator, TResult>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TValue> valueSelector, TAccumulator accumulator, Func<TAccumulator, TResult> resultSelector)
        where TObject : notnull
        where TAccumulator : struct, IAccumulator<TValue>
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(valueSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        return Observable.Defer(() =>
        {
            var state = accumulator;
            var values = new List<(TObject Item, TValue Value)>();

            return source.Select(changes =>
            {
                var isValid = true;

                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            {
                                var value = valueSelector(change.Item.Current);
                                if (change.Item.CurrentIndex < 0 || change.Item.CurrentIndex >= values.Count)
                                {
                                    values.Add((change.Item.Current, value));
                                }
                                else
                                {
                                    values.Insert(change.Item.CurrentIndex, (change.Item.Current, value));
                                }

                                state.Add(value);
                                break;
                            }

                        case ListChangeReason.AddRange:
                            {
                                var entries = new List<(TObject Item, TValue Value)>(change.Range.Count);
                                foreach (var item in change.Range)
                                {
                                    var value = valueSelector(item);
                                    entries.Add((item, value));
                                    state.Add(value);
                                }

                                if (change.Range.Index < 0 || change.Range.Index >= values.Count)
                                {
                                    values.AddRange(entries);
                                }
                                else
                                {
                                    values.InsertRange(change.Range.Index, entries);
                                }

                                break;
                            }

                        case ListChangeReason.Replace:
                            {
                                var previousIndex = FindIndex(values, change.Item.PreviousIndex, change.Item.Previous.Value);
                                var previous = values[previousIndex].Value;
                                var current = valueSelector(change.Item.Current);

                                if (change.Item.CurrentIndex < 0 || change.Item.CurrentIndex == previousIndex)
                                {
                                    values[previousIndex] = (change.Item.Current, current);
                                }
                                else
                                {
                                    values.RemoveAt(previousIndex);
                                    values.Insert(change.Item.CurrentIndex, (change.Item.Current, current));
                                }

                                isValid &= state.Replace(previous, current);
                                break;
                            }

                        case ListChangeReason.Refresh:
                            {
                                var index = FindIndex(values, change.Item.CurrentIndex, change.Item.Current);
                                var previous = values[index].Value;
                                var current = valueSelector(change.Item.Current);
                                values[index] = (change.Item.Current, current);
                                isValid &= state.Replace(previous, current);
                                break;
                            }

                        case ListChangeReason.Remove:
                            {
                                var index = FindIndex(values, change.Item.CurrentIndex, change.Item.Current);
                                var value = values[index].Value;
                                values.RemoveAt(index);
                                isValid &= state.Remove(value);
                                break;
                            }

                        case ListChangeReason.RemoveRange:
                            if (change.Range.Index >= 0)
                            {
                                var end = change.Range.Index + change.Range.Count;
                                for (var index = change.Range.Index; index < end; ++index)
                                {
                                    isValid &= state.Remove(values[index].Value);
                                }

                                values.RemoveRange(change.Range.Index, change.Range.Count);
                            }
                            else
                            {
                                foreach (var item in change.Range)
                                {
                                    var index = FindIndex(values, -1, item);
                                    isValid &= state.Remove(values[index].Value);
                                    values.RemoveAt(index);
                                }
                            }

                            break;

                        case ListChangeReason.Clear:
                            values.Clear();
                            state.Reset();
                            isValid = true;
                            break;

                        case ListChangeReason.Moved:
                            {
                                var entry = values[change.Item.PreviousIndex];
                                values.RemoveAt(change.Item.PreviousIndex);
                                values.Insert(change.Item.CurrentIndex, entry);
                                break;
                            }
                    }
                }

                if (!isValid)
                {
                    state.Reset();
                    foreach (var entry in values)
                    {
                        state.Add(entry.Value);
                    }
                }

                return resultSelector(state);
            });
        });

        static int FindIndex(List<(TObject Item, TValue Value)> values, int index, TObject item)
        {
            if (index >= 0)
            {
                return index;
            }

            for (var i = 0; i < values.Count; ++i)
            {
                if (ReferenceEquals(values[i].Item, item))
                {
                    return i;
                }
            }

            var comparer = EqualityComparer<TObject>.Default;
            for (var i = 0; i < values.Count; ++i)
            {
                if (comparer.Equals(values[i].Item, item))
                {
                    return i;
                }
            }

            throw new UnspecifiedIndexException($"Cannot find index of {item}");
        }
    }

    /// <summary>
    /// Applies an accumulator when items are added to and removed from specified stream,
    /// starting with the initial seed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="accessor">The accessor.</param>
    /// <param name="addAction">The add action.</param>
    /// <param name="removeAction">The remove action.</param>
    /// <returns>An observable with the accumulated value.</returns>
    internal static IObservable<TResult> Accumulate<TObject, TResult>(this IObservable<IChangeSet<TObject>> source, TResult seed, Func<TObject, TResult> accessor, Func<TResult, TResult, TResult> addAction, Func<TResult, TResult, TResult> removeAction)
        where TObject : notnull => source.ForAggregation().Accumulate(seed, accessor, addAction, removeAction);

    /// <summary>
    /// Applies an accumulator when items are added to and removed from specified stream,
    /// starting with the initial seed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="accessor">The accessor.</param>
    /// <param name="addAction">The add action.</param>
    /// <param name="removeAction">The remove action.</param>
    /// <returns>An observable with the accumulated value.</returns>
    internal static IObservable<TResult> Accumulate<TObject, TKey, TResult>(this IObservable<IChangeSet<TObject, TKey>> source, TResult seed, Func<TObject, TResult> accessor, Func<TResult, TResult, TResult> addAction, Func<TResult, TResult, TResult> removeAction)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Accumulate(seed, accessor, addAction, removeAction);

    /// <summary>
    /// Applies an accumulator when items are added to and removed from specified stream,
    /// starting with the initial seed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="accessor">The accessor.</param>
    /// <param name="addAction">The add action.</param>
    /// <param name="removeAction">The remove action.</param>
    /// <returns>An observable with the accumulated value.</returns>
    internal static IObservable<TResult> Accumulate<TObject, TResult>(this IObservable<IAggregateChangeSet<TObject>> source, TResult seed, Func<TObject, TResult> accessor, Func<TResult, TResult, TResult> addAction, Func<TResult, TResult, TResult> removeAction)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(addAction);
        ArgumentNullException.ThrowIfNull(removeAction);

        return source.Scan(seed, (state, changes) =>
            changes.Aggregate(state, (current, aggregateItem) =>
                aggregateItem.Type == AggregateType.Add ? addAction(current, accessor(aggregateItem.Item)) : removeAction(current, accessor(aggregateItem.Item))));
    }
}
