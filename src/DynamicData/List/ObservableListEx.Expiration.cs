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
/// ObservableList extensions for ExpireAfter, LimitSizeTo, and Top.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Automatically removes items from the <paramref name="source"/> list after the duration returned by <paramref name="timeSelector"/>.
    /// Returns an observable of the items that were expired and removed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <see cref="ISourceList{T}"/> source list to apply time-based expiration to.</param>
    /// <param name="timeSelector">A <see cref="Func{T, TResult}"/> function returning the time-to-live for each item. Return <see langword="null"/> for items that should never expire.</param>
    /// <param name="pollingInterval">An optional <see cref="TimeSpan"/> polling interval to batch expiry checks. If omitted, a separate timer is created for each unique expiry time.</param>
    /// <param name="scheduler">The scheduler for scheduling expiry timers. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits collections of items each time expired items are removed from the source list.</returns>
    /// <remarks>
    /// <para>
    /// This operator acts directly on an <see cref="ISourceList{T}"/>, not on a changeset stream. It monitors items as they are added,
    /// schedules their removal, and physically removes them from the source list when their time expires.
    /// </para>
    /// <para>
    /// When <paramref name="pollingInterval"/> is specified, all items due for removal are batched into a single removal at each polling tick,
    /// which can improve performance when many items expire around the same time.
    /// </para>
    /// <para><b>Worth noting:</b> The returned observable emits the expired items (not changesets). Subscribe to this observable to trigger the expiry mechanism; if not subscribed, no items will be removed.</para>
    /// </remarks>
    /// <seealso cref="LimitSizeTo{T}(ISourceList{T}, int, IScheduler?)"/>
    /// <seealso cref="ToObservableChangeSet{T}(IObservable{T}, Func{T, TimeSpan?}, IScheduler?)"/>
    public static IObservable<IEnumerable<T>> ExpireAfter<T>(
                this ISourceList<T> source,
                Func<T, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval = null,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ExpireAfter<T>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval,
            scheduler: scheduler);

    /// <summary>
    /// Limits the source list to a maximum number of items using FIFO eviction.
    /// When the list exceeds <paramref name="sizeLimit"/>, the oldest items are removed.
    /// Returns an observable of the items that were removed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <see cref="ISourceList{T}"/> source list to apply size limits to.</param>
    /// <param name="sizeLimit">The maximum number of items allowed. Must be greater than zero.</param>
    /// <param name="scheduler">The scheduler for scheduling size checks. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits collections of items each time excess items are removed from the source list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sizeLimit"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// This operator acts directly on an <see cref="ISourceList{T}"/>. It subscribes to the source's changes,
    /// tracks insertion order using an internal Transform, and removes the oldest items when the size limit is exceeded.
    /// </para>
    /// <para><b>Worth noting:</b> The returned observable emits the removed items (not changesets). Subscribe to this observable to activate the size-limiting mechanism. Removal is performed synchronously under a lock shared with the change tracking.</para>
    /// </remarks>
    /// <seealso cref="ExpireAfter{T}(ISourceList{T}, Func{T, TimeSpan?}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="Top{T}(IObservable{IChangeSet{T}}, int)"/>
    public static IObservable<IEnumerable<T>> LimitSizeTo<T>(this ISourceList<T> source, int sizeLimit, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (sizeLimit <= 0)
        {
            throw new ArgumentException("sizeLimit cannot be zero", nameof(sizeLimit));
        }

        var locker = InternalEx.NewLock();
        var limiter = new LimitSizeTo<T>(source, sizeLimit, scheduler ?? GlobalConfig.DefaultScheduler, locker);

        return limiter.Run().Synchronize(locker).Do(source.RemoveMany);
    }

    /// <summary>
    /// Takes the first <paramref name="numberOfItems"/> items from the source list. Implemented as <c>Virtualise</c> with a fixed window starting at index 0.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to take the top items.</param>
    /// <param name="numberOfItems">The maximum number of items to include. Must be greater than zero.</param>
    /// <returns>A virtual changeset stream containing at most <paramref name="numberOfItems"/> items from the beginning of the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfItems"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>The source should ideally be sorted before applying Top, since list order determines which items appear.</para>
    /// </remarks>
    /// <seealso cref="Virtualise{T}(IObservable{IChangeSet{T}}, IObservable{IVirtualRequest})"/>
    /// <seealso cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/>
    /// <seealso cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    public static IObservable<IChangeSet<T>> Top<T>(this IObservable<IChangeSet<T>> source, int numberOfItems)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (numberOfItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items should be greater than zero");
        }

        return source.Virtualise(Observable.Return(new VirtualRequest(0, numberOfItems)));
    }
}
