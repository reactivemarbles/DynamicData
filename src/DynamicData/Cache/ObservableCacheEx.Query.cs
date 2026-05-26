// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// ObservableCache extensions for querying, snapshot collection projection, and conversion to changeset streams.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    ///     Selects distinct values from the source.
    /// </summary>
    /// <typeparam name="TObject">The type object from which the distinct values are selected.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to extract distinct values.</param>
    /// <param name="valueSelector">The <see cref="Func{TObject, TValue}"/> value selector.</param>
    /// <returns>An observable which will emit distinct change sets.</returns>
    /// <remarks>
    /// Due to it's nature only adds or removes can be returned.
    /// <para><b>Worth noting:</b> Reference counting assumes value equality is transitive. Mutable value objects with inconsistent <c>Equals</c> implementations can corrupt ref counts.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <seealso cref="ObservableListEx.DistinctValues"/>
    public static IObservable<IDistinctChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return Observable.Create<IDistinctChangeSet<TValue>>(observer => new DistinctCalculator<TObject, TKey, TValue>(source, valueSelector).Run().SubscribeSafe(observer));
    }

    /// <summary>
    /// Projects the current cache state through <paramref name="resultSelector"/> after each modification.
    /// Emits a new value of <typeparamref name="TDestination"/> on every changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to project on each change.</param>
    /// <param name="resultSelector">A function that projects the current <see cref="IQuery{TObject, TKey}"/> snapshot to a result value.</param>
    /// <returns>An observable that emits a projected value after each changeset.</returns>
    /// <remarks>
    /// <para><b>Worth noting:</b> The selector is called on every changeset, which can be chatty. The <see cref="IQuery{TObject, TKey}"/> exposes the full cache state for LINQ-style queries.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="resultSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ToCollection{TObject, TKey}"/>
    /// <seealso cref="ToSortedCollection{TObject, TKey, TSortKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TSortKey}, SortDirection)"/>
    /// <seealso cref="ObservableListEx.QueryWhenChanged"/>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<IQuery<TObject, TKey>, TDestination> resultSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to project on each change.</param>
    /// <returns>An observable which emits the query.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new QueryWhenChanged<TObject, TKey, Unit>(source).Run();
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) on subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to project on each change.</param>
    /// <param name="itemChangedTrigger">A <see cref="Func{T, TResult}"/> that should the query be triggered for observables on individual items.</param>
    /// <returns>An observable that emits the query.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> itemChangedTrigger)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        itemChangedTrigger.ThrowArgumentNullExceptionIfNull(nameof(itemChangedTrigger));

        return new QueryWhenChanged<TObject, TKey, TValue>(source, itemChangedTrigger).Run();
    }

    /// <summary>
    /// Converts the change set into a fully formed collection. Each change in the source results in a new collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to materialize into a collection on each change.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    /// <seealso cref="ObservableListEx.ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.QueryWhenChanged(query => new ReadOnlyCollectionLight<TObject>(query.Items));

    /// <summary>
    /// Bridges a standard Rx observable of individual items into a DynamicData changeset stream.
    /// Each emission becomes an <b>Add</b> (or <b>Update</b> if the key already exists).
    /// Supports optional per-item expiration and size limiting.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{TObject}"/> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that selects the unique key for each item.</param>
    /// <param name="expireAfter">An optional <see cref="Func{T, TResult}"/> that specifies per-item expiration time. Return <see langword="null"/> for no expiration.</param>
    /// <param name="limitSizeTo">The maximum cache size. Oldest items are removed when exceeded. Use -1 for no limit.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiration timing.</param>
    /// <returns>An observable changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(
            this IObservable<TObject> source,
            Func<TObject, TKey> keySelector,
            Func<TObject, TimeSpan?>? expireAfter = null,
            int limitSizeTo = -1,
            IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return Cache.Internal.ToObservableChangeSet<TObject, TKey>.Create(
            source: source,
            keySelector: keySelector,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);
    }

    /// <summary>
    /// Bridges a standard Rx observable of item batches into a DynamicData changeset stream.
    /// Each batch is processed with <c>AddOrUpdate</c>, producing <b>Add</b> or <b>Update</b> changes per item.
    /// Supports optional per-item expiration and size limiting.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IEnumerable{TObject}}"/> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that selects the unique key for each item.</param>
    /// <param name="expireAfter">An optional <see cref="Func{T, TResult}"/> that specifies per-item expiration time. Return <see langword="null"/> for no expiration.</param>
    /// <param name="limitSizeTo">The maximum cache size. Oldest items are removed when exceeded. Use -1 for no limit.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiration timing.</param>
    /// <returns>An observable changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(
            this IObservable<IEnumerable<TObject>> source,
            Func<TObject, TKey> keySelector,
            Func<TObject, TimeSpan?>? expireAfter = null,
            int limitSizeTo = -1,
            IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return Cache.Internal.ToObservableChangeSet<TObject, TKey>.Create(
            source: source,
            keySelector: keySelector,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);
    }

    /// <summary>
    /// Watches a single key in the source changeset stream, emitting <c>Optional.Some(value)</c> when the key
    /// is present and <see cref="Optional.None{T}"/> when it is removed. Duplicate values are suppressed via <paramref name="equalityComparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to watch.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that optional comparer to suppress duplicate emissions. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable of <see cref="Optional{TObject}"/> that reflects the presence or absence of the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>, this emits <c>None</c> on removal
    /// (rather than the removed value), making it possible to distinguish "key is absent" from "key has a value".
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits <c>Optional.Some(value)</c> if the key was not previously tracked.</description></item>
    /// <item><term>Update</term><description>Emits <c>Optional.Some(newValue)</c> if the new value differs from the previous per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// <item><term>Remove</term><description>Emits <see cref="Optional.None{T}"/>.</description></item>
    /// <item><term>Refresh</term><description>Emits <c>Optional.Some(value)</c> if the value differs from the last emission per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. To get an initial <c>None</c> when the key is absent, use the overload with <c>initialOptionalWhenMissing: true</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Watch{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    public static IObservable<Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new ToObservableOptional<TObject, TKey>(source, key, equalityComparer).Run();
    }

    /// <summary>
    /// Converts an observable cache into an observable optional that emits the value for the given key.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key value.</param>
    /// <param name="initialOptionalWhenMissing">When <see langword="true"/>, emits an initial <see cref="Optional{TObject}"/> with no value if the key is not present in the cache.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> instance used to determine if an object value has changed.</param>
    /// <returns>An observable optional.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Uses lock-based coordination. If the key exists synchronously on <c>Connect()</c>, the initial <c>None</c> may or may not be emitted depending on timing.</para>
    /// </remarks>
    public static IObservable<Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, bool initialOptionalWhenMissing, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        if (initialOptionalWhenMissing)
        {
            return Observable.Defer(() =>
            {
                var seenValue = false;
                return source.ToObservableOptional(key, equalityComparer)
                    .Do(_ => seenValue = true)
                    .Merge(Observable.Defer(() => seenValue
                        ? Observable.Empty<Optional<TObject>>()
                        : Observable.Return(Optional.None<TObject>())));
            });
        }

        return source.ToObservableOptional(key, equalityComparer);
    }

    /// <summary>
    /// Converts the change set into a fully formed sorted collection. Each change in the source results in a new sorted collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TSortKey">The sort key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to materialize into a sorted collection on each change.</param>
    /// <param name="sort">The <see cref="Func{TObject, TSortKey}"/> sort function.</param>
    /// <param name="sortOrder">The <see cref="SortDirection"/> sort order. Defaults to ascending.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    /// <seealso cref="ObservableListEx.ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TKey, TSortKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
        where TObject : notnull
        where TKey : notnull
        where TSortKey : notnull => source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.Items.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.Items.OrderByDescending(sort)));

    /// <summary>
    /// Converts the change set into a fully formed sorted collection. Each change in the source results in a new sorted collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to materialize into a sorted collection on each change.</param>
    /// <param name="comparer">The <see cref="IComparer{TObject}"/> sort comparer.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull => source.QueryWhenChanged(
            query =>
            {
                var items = query.Items.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });

    private static IObservable<bool> TrueFor<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => new TrueFor<TObject, TKey, TValue>(source, observableSelector, collectionMatcher).Run();

    /// <summary>
    /// Emits <see langword="true"/> when all items in the cache satisfy a condition based on their per-item observable,
    /// and <see langword="false"/> otherwise. Re-evaluates whenever the cache changes or any per-item observable emits.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value emitted by each per-item observable.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to evaluate a condition across all items in.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{T, TResult}"/> that predicate applied to each per-item observable's latest value.</param>
    /// <returns>An observable of <c>bool</c> that emits whenever the all-items condition changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="equalityCondition"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>A new per-item subscription is created. The aggregate condition is recalculated.</description></item>
    /// <item><term>Update</term><description>The item is replaced in the collection snapshot. Condition recalculated.</description></item>
    /// <item><term>Remove</term><description>Per-item subscription disposed. Condition recalculated over remaining items.</description></item>
    /// <item><term>Refresh</term><description>No effect on per-item subscriptions. Condition not recalculated unless the per-item observable emits.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Items whose per-item observable has not yet emitted are treated as not satisfying the condition. An empty cache is vacuously <see langword="true"/>. The result uses <c>DistinctUntilChanged</c>, so duplicate <c>bool</c> values are suppressed.</para>
    /// </remarks>
    /// <seealso cref="TrueForAny{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TValue, bool})"/>
    public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));

    /// <summary>
    /// <para>
    /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
    /// the equality condition. The observable is re-evaluated whenever.
    /// </para>
    /// <para>
    /// i) The cache changes
    /// or ii) The inner observable changes.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to evaluate a condition across all items in.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> that selector which returns the target observable.</param>
    /// <param name="equalityCondition">The <see cref="Func{TObject, TValue, bool}"/> equality condition.</param>
    /// <returns>An observable which boolean values indicating if true.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));

    /// <summary>
    /// Emits <see langword="true"/> when any item in the cache satisfies a condition based on its per-item observable,
    /// and <see langword="false"/> when none do. Re-evaluates whenever the cache changes or any per-item observable emits.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value emitted by each per-item observable.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to evaluate a condition across any item in.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{T, TResult}"/> that predicate applied to each item and its per-item observable's latest value.</param>
    /// <returns>An observable of <c>bool</c> that emits whenever the any-item condition changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="equalityCondition"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>A new per-item subscription is created. The aggregate condition is recalculated.</description></item>
    /// <item><term>Update</term><description>The item is replaced in the collection snapshot. Condition recalculated.</description></item>
    /// <item><term>Remove</term><description>Per-item subscription disposed. Condition recalculated over remaining items.</description></item>
    /// <item><term>Refresh</term><description>No effect on per-item subscriptions. Condition not recalculated unless the per-item observable emits.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Items whose per-item observable has not yet emitted are treated as not satisfying the condition. An empty cache yields <see langword="false"/>. The result uses <c>DistinctUntilChanged</c>, so duplicate <c>bool</c> values are suppressed.</para>
    /// </remarks>
    /// <seealso cref="TrueForAll{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TValue, bool})"/>
    public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));

    /// <inheritdoc cref="TrueForAny{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TObject, TValue, bool})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to evaluate a condition across any item in.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{T, TResult}"/> that predicate applied to each per-item observable's latest value (without the item).</param>
    /// <remarks>This overload accepts a predicate that takes only the value, not the item. Useful when the condition depends only on the observed value.</remarks>
    public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        equalityCondition.ThrowArgumentNullExceptionIfNull(nameof(equalityCondition));

        return source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
    }
}
