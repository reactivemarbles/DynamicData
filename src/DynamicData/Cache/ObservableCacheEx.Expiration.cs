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
/// ObservableCache extensions for ExpireAfter and LimitSizeTo.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Schedules automatic removal of items after the timeout returned by <paramref name="timeSelector"/>.
    /// If <paramref name="timeSelector"/> returns <see langword="null"/>, the item never expires.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to apply time-based expiration to.</param>
    /// <param name="timeSelector">An optional <see cref="Func{T, TResult}"/> that returns the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <returns>An observable changeset that includes timer-driven <b>Remove</b> changes for expired items.</returns>
    /// <remarks>
    /// <para>When a timer fires, a <b>Remove</b> is emitted for the expired item.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Schedules a removal timer based on <paramref name="timeSelector"/>. Forwarded as <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>Resets the removal timer for the item. Forwarded as <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Cancels the removal timer. Forwarded as <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b>. No timer change.</description></item>
    /// <item><term>OnError</term><description>All pending timers are cancelled.</description></item>
    /// <item><term>OnCompleted</term><description>All pending timers are cancelled.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> A <see langword="null"/> return from <paramref name="timeSelector"/> means "never expire". <b>Update</b> changes reset the expiration timer.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="timeSelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector);

    /// <inheritdoc cref="ExpireAfter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TimeSpan?})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to apply time-based expiration to.</param>
    /// <param name="timeSelector">An optional <see cref="Func{T, TResult}"/> that returns the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used to schedule expiration timers.</param>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector,
                IScheduler scheduler)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            scheduler: scheduler);

    /// <inheritdoc cref="ExpireAfter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TimeSpan?})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to apply time-based expiration to.</param>
    /// <param name="timeSelector">An optional <see cref="Func{T, TResult}"/> that returns the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="pollingInterval">An optional <see cref="TimeSpan"/> polling interval. If specified, items are expired on a polling interval rather than per-item timers. Less accurate but more efficient when many items share similar expiration times.</param>
    /// <remarks>
    /// This overload uses periodic polling instead of per-item timers. Expired items are removed on the next
    /// poll after their timeout elapses, which trades accuracy for reduced timer overhead.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval);

    /// <inheritdoc cref="ExpireAfter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TimeSpan?}, TimeSpan?)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to apply time-based expiration to.</param>
    /// <param name="timeSelector">An optional <see cref="Func{T, TResult}"/> that returns the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="pollingInterval">An optional <see cref="TimeSpan"/> if specified, items are expired on a polling interval rather than per-item timers.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used to schedule polling and expiration timers.</param>
    public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Func<TObject, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval,
                IScheduler scheduler)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForStream<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval,
            scheduler: scheduler);

    /// <summary>
    /// Automatically removes items from the <see cref="ISourceCache{TObject, TKey}"/> after the timeout returned
    /// by <paramref name="timeSelector"/>. Returns an observable of the removed key-value pairs (not a changeset stream).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to operate on.</param>
    /// <param name="timeSelector">An optional <see cref="Func{T, TResult}"/> that returns the expiration timeout for each item, or <see langword="null"/> for no expiration.</param>
    /// <param name="pollingInterval">An optional <see cref="TimeSpan"/> if specified, items are expired on a polling interval rather than per-item timers.</param>
    /// <param name="scheduler">The scheduler used to schedule expiration timers. Defaults to <see cref="GlobalConfig.DefaultScheduler"/> if <see langword="null"/>.</param>
    /// <returns>An observable that emits the key-value pairs of items removed from the cache by expiration.</returns>
    /// <remarks>
    /// Unlike the stream-based overloads, this operates directly on the <see cref="ISourceCache{TObject, TKey}"/>
    /// and returns the removed items as <see cref="KeyValuePair{TKey, TObject}"/> collections,
    /// not as a changeset stream.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="timeSelector"/> is <see langword="null"/>.</exception>
    public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(
                this ISourceCache<TObject, TKey> source,
                Func<TObject, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval = null,
                IScheduler? scheduler = null)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.ExpireAfter.ForSource<TObject, TKey>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval,
            scheduler: scheduler);

    /// <summary>
    /// Applies a FIFO size limit to the changeset stream. When the number of items exceeds <paramref name="size"/>,
    /// the oldest items are evicted and emitted as <b>Remove</b> changes.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to apply size limits to.</param>
    /// <param name="size">The maximum number of items allowed. Must be greater than zero.</param>
    /// <returns>An observable changeset stream with size-limited contents.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>Forwarded. If the cache exceeds the size limit, the oldest items are emitted as <b>Remove</b> changes.</description></item>
    ///   <item><term><b>Update</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Remove</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Refresh</b></term><description>Forwarded unchanged.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="size"/> is zero or negative.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> LimitSizeTo<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, int size)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (size <= 0)
        {
            throw new ArgumentException("Size limit must be greater than zero");
        }

        return new SizeExpirer<TObject, TKey>(source, size).Run();
    }

    /// <summary>
    /// Operates directly on a <see cref="ISourceCache{TObject, TKey}"/>, removing the oldest items when the cache
    /// exceeds <paramref name="sizeLimit"/>. Returns an observable of the evicted key-value pairs (not a changeset stream).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to operate on.</param>
    /// <param name="sizeLimit">The maximum number of items allowed. Must be greater than zero.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for observing changes. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits batches of evicted key-value pairs whenever the cache exceeds the size limit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sizeLimit"/> is zero or negative.</exception>
    public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> LimitSizeTo<TObject, TKey>(this ISourceCache<TObject, TKey> source, int sizeLimit, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (sizeLimit <= 0)
        {
            throw new ArgumentException("Size limit must be greater than zero", nameof(sizeLimit));
        }

        return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(
            observer =>
            {
                long orderItemWasAdded = -1;
                var sizeLimiter = new SizeLimiter<TObject, TKey>(sizeLimit);

                return source.Connect().Finally(observer.OnCompleted).ObserveOn(scheduler ?? GlobalConfig.DefaultScheduler).Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now, Interlocked.Increment(ref orderItemWasAdded))).Select(sizeLimiter.CloneAndReturnExpiredOnly).Where(expired => expired.Length != 0).Subscribe(
                    toRemove =>
                    {
                        try
                        {
                            source.Remove(toRemove.Select(kv => kv.Key));
                            observer.OnNext(toRemove);
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    });
            });
    }
}
