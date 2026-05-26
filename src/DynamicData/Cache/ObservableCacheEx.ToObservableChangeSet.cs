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
/// ObservableCache extensions for converting plain observables into changeset streams.
/// </summary>
public static partial class ObservableCacheEx
{
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
            var seenValue = false;
            var locker = InternalEx.NewLock();

            var optional = source.ToObservableOptional(key, equalityComparer).Synchronize(locker).Do(_ => seenValue = true);
            var missing = Observable.Return(Optional.None<TObject>()).Synchronize(locker).Where(_ => !seenValue);

            return optional.Merge(missing);
        }

        return source.ToObservableOptional(key, equalityComparer);
    }
}
