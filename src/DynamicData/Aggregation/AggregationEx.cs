// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        accessor.ThrowArgumentNullExceptionIfNull(nameof(accessor));
        addAction.ThrowArgumentNullExceptionIfNull(nameof(addAction));
        removeAction.ThrowArgumentNullExceptionIfNull(nameof(removeAction));

        return source.Scan(seed, (state, changes) =>
            changes.Aggregate(state, (current, aggregateItem) =>
                aggregateItem.Type == AggregateType.Add ? addAction(current, accessor(aggregateItem.Item)) : removeAction(current, accessor(aggregateItem.Item))));
    }
}
