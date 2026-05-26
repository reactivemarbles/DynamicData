// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableList extensions for querying and snapshot collection projection.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Emits a projected value from the current list snapshot after every changeset.
    /// The <paramref name="resultSelector"/> receives an <see cref="IReadOnlyCollection{T}"/> representing the current state.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TDestination">The type of the projected result.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to project on each change.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> function projecting the current list snapshot to a result value.</param>
    /// <returns>An observable emitting the projected value after each changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="resultSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Delegates to <see cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/> and applies <paramref name="resultSelector"/> via <c>Select</c>.</para>
    /// </remarks>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ObservableCacheEx.QueryWhenChanged{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{IQuery{TObject, TKey}, TDestination})"/>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<IReadOnlyCollection<TObject>, TDestination> resultSelector)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// Emits an <see cref="IReadOnlyCollection{T}"/> snapshot of the current list state after every changeset.
    /// Maintains an internal list updated by cloning each changeset.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to project on each change.</param>
    /// <returns>An observable emitting the full list snapshot as <see cref="IReadOnlyCollection{T}"/> after each change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a non-changeset operator. It emits the entire collection state on each change, not incremental diffs.</para>
    /// <para><b>Worth noting:</b> A new snapshot is emitted on every changeset, which can be chatty. The collection is rebuilt by cloning each changeset into an internal list. For sorted output, use <see cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>.</para>
    /// </remarks>
    /// <seealso cref="QueryWhenChanged{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{IReadOnlyCollection{TObject}, TDestination})"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    public static IObservable<IReadOnlyCollection<T>> QueryWhenChanged<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new QueryWhenChanged<T>(source).Run();
    }

    /// <summary>
    /// Emits the full collection as an <see cref="IReadOnlyCollection{T}"/> after every changeset. Equivalent to <c>QueryWhenChanged(items => items)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to materialize into a collection on each change.</param>
    /// <returns>An observable emitting the full collection snapshot after each change.</returns>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    /// <seealso cref="ObservableCacheEx.ToCollection{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.QueryWhenChanged(items => items);

    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into the DynamicData world by converting each emitted item into a list changeset.
    /// Each emission becomes an <b>Add</b> operation in the resulting changeset stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> to convert into a changeset stream.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for time-based operations (expiry, size limiting).</param>
    /// <returns>A list changeset stream where each source emission is an <b>Add</b>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the primary bridge from standard Rx into DynamicData's list changeset model. Each item emitted by <paramref name="source"/>
    /// is added to an internal list and an <b>Add</b> changeset is emitted. The list grows unboundedly unless size or time limits
    /// are specified via other overloads.
    /// </para>
    /// <para><b>Worth noting:</b> Source completion and errors are propagated. The internal list is disposed on unsubscribe.</para>
    /// </remarks>
    /// <seealso cref="ToObservableChangeSet{T}(IObservable{T}, Func{T, TimeSpan?}, int, IScheduler?)"/>
    /// <seealso cref="ToObservableChangeSet{T}(IObservable{IEnumerable{T}}, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into a list changeset stream with per-item time-based expiry.
    /// Expired items are automatically removed.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                Func<T, TimeSpan?> expireAfter,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into a list changeset stream with FIFO size limiting.
    /// When the list exceeds <paramref name="limitSizeTo"/>, the oldest items are removed.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> to convert into a changeset stream.</param>
    /// <param name="limitSizeTo">The maximum list size. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling removals.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> into a list changeset stream with both time-based expiry and FIFO size limiting.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="limitSizeTo">The maximum list size. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers and size-limit checks.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                Func<T, TimeSpan?>? expireAfter,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream.
    /// Each emitted batch becomes an <b>AddRange</b>.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/> to convert into a changeset stream.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for time-based operations.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream with FIFO size limiting.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/> to convert into a changeset stream.</param>
    /// <param name="limitSizeTo">The maximum list size. Oldest items are removed when the limit is exceeded.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling removals.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream with time-based expiry.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                Func<T, TimeSpan?> expireAfter,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <inheritdoc cref="ToObservableChangeSet{T}(IObservable{T}, IScheduler?)"/>
    /// <summary>
    /// Bridges an <see cref="IObservable{T}"/> of <see cref="IEnumerable{T}"/> batches into a list changeset stream with both time-based expiry and FIFO size limiting.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{T}}"/> of <see cref="IEnumerable{T}"/> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="limitSizeTo">The maximum list size. Oldest items removed when exceeded.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers and size-limit checks.</param>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                Func<T, TimeSpan?>? expireAfter,
                int limitSizeTo,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);

    /// <summary>
    /// Emits a sorted <see cref="IReadOnlyCollection{T}"/> after every changeset, sorted by the value returned by <paramref name="sort"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TSortKey">The type of the sort key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to materialize into a sorted collection on each change.</param>
    /// <param name="sort">A <see cref="Func{T, TResult}"/> function extracting the sort key from each item.</param>
    /// <param name="sortOrder">The <see cref="SortDirection"/> sort direction. Defaults to ascending.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ToSortedCollection{TObject}(IObservable{IChangeSet{TObject}}, IComparer{TObject})"/>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ObservableCacheEx.ToSortedCollection{TObject, TKey}"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TSortKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
        where TObject : notnull => source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.OrderByDescending(sort)));

    /// <summary>
    /// Emits a sorted <see cref="IReadOnlyCollection{T}"/> after every changeset, sorted using the specified <paramref name="comparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to materialize into a sorted collection on each change.</param>
    /// <param name="comparer">The <see cref="IComparer{TObject}"/> used for sorting.</param>
    /// <returns>An observable emitting a sorted collection snapshot after each change.</returns>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject>(this IObservable<IChangeSet<TObject>> source, IComparer<TObject> comparer)
        where TObject : notnull => source.QueryWhenChanged(
            query =>
            {
                var items = query.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });
}
