// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests;

/// <summary>
/// Test extensions.
/// </summary>
public static class TestEx
{
    /// <summary>
    /// Aggregates all events and statistics for a paged change set to help assertions when testing.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The change set aggregator.</returns>
    public static ChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => new(source);

    /// <summary>
    /// Aggregates all events and statistics for a paged change set to help assertions when testing.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TContext">The type of context.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The change set aggregator.</returns>
    public static ChangeSetAggregator<TObject, TKey, TContext> AsAggregator<TObject, TKey, TContext>(this IObservable<IChangeSet<TObject, TKey, TContext>> source)
        where TObject : notnull
        where TKey : notnull => new(source);

    /// <summary>
    /// Aggregates all events and statistics for a distinct change set to help assertions when testing.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The distinct change set aggregator.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static DistinctChangeSetAggregator<TValue> AsAggregator<TValue>(this IObservable<IDistinctChangeSet<TValue>> source)
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DistinctChangeSetAggregator<TValue>(source);
    }

    /// <summary>
    /// Aggregates all events and statistics for a group change set to help assertions when testing.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the grouping key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The distinct change set aggregator.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static GroupChangeSetAggregator<TValue, TKey, TGroupKey> AsAggregator<TValue, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TValue, TKey, TGroupKey>> source)
        where TValue : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new GroupChangeSetAggregator<TValue, TKey, TGroupKey>(source);
    }

    /// <summary>
    /// Aggregates all events and statistics for a sorted change set to help assertions when testing.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The sorted change set aggregator.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static SortedChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new SortedChangeSetAggregator<TObject, TKey>(source);
    }

    /// <summary>
    /// Aggregates all events and statistics for a virtual change set to help assertions when testing.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The virtual change set aggregator.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static VirtualChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new VirtualChangeSetAggregator<TObject, TKey>(source);
    }

    /// <summary>
    /// Aggregates all events and statistics for a paged change set to help assertions when testing.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The paged change set aggregator.</returns>
    public static PagedChangeSetAggregator<TObject, TKey> AsAggregator<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new PagedChangeSetAggregator<TObject, TKey>(source);
    }
}
