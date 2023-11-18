// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Aggregation;

/// <summary>
/// Count extensions.
/// </summary>
public static class CountEx
{
    /// <summary>
    /// Counts the total number of items in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<int> Count<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Count();

    /// <summary>
    /// Counts the total number of items in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<int> Count<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.ForAggregation().Count();

    /// <summary>
    /// Counts the total number of items in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<int> Count<TObject>(this IObservable<IAggregateChangeSet<TObject>> source) => source.Accumulate(0, _ => 1, (current, increment) => current + increment, (current, increment) => current - increment);

    /// <summary>
    /// Counts the total number of items in the underlying data source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<int> Count<TObject>(this IObservable<IDistinctChangeSet<TObject>> source)
        where TObject : notnull => source.ForAggregation().Count();

    /// <summary>
    /// Counts the total number of items in the underlying data source
    /// and return true if the number of items == 0.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<bool> IsEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Count().StartWith(0).Select(count => count == 0);

    /// <summary>
    /// Counts the total number of items in the underlying data source
    /// and return true if the number of items == 0.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<bool> IsEmpty<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.ForAggregation().Count().StartWith(0).Select(count => count == 0);

    /// <summary>
    /// Counts the total number of items in the underlying data source
    /// and returns true if the number of items is greater than 0.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<bool> IsNotEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.ForAggregation().Count().StartWith(0).Select(count => count > 0);

    /// <summary>
    /// Counts the total number of items in the underlying data source
    /// and returns true if the number of items is greater than 0.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the count.</returns>
    public static IObservable<bool> IsNotEmpty<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.ForAggregation().Count().StartWith(0).Select(count => count > 0);
}
