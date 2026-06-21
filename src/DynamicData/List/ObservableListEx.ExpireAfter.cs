// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Automatically removes items from the <paramref name="source"/> list after the duration returned by <paramref name="timeSelector"/>.
    /// Returns an observable of the items that were expired and removed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <c>ISourceList&lt;T&gt;</c> source list to apply time-based expiration to.</param>
    /// <param name="timeSelector">A <c>Func&lt;T, TResult&gt;</c> function returning the time-to-live for each item. Return <see langword="null"/> for items that should never expire.</param>
    /// <param name="pollingInterval">An optional <see cref="TimeSpan"/> polling interval to batch expiry checks. If omitted, a separate timer is created for each unique expiry time.</param>
    /// <param name="scheduler">The scheduler for scheduling expiry timers. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits collections of items each time expired items are removed from the source list.</returns>
    /// <remarks>
    /// <para>
    /// This operator acts directly on an <c>ISourceList&lt;T&gt;</c>, not on a changeset stream. It monitors items as they are added,
    /// schedules their removal, and physically removes them from the source list when their time expires.
    /// </para>
    /// <para>
    /// When <paramref name="pollingInterval"/> is specified, all items due for removal are batched into a single removal at each polling tick,
    /// which can improve performance when many items expire around the same time.
    /// </para>
    /// <para><b>Worth noting:</b> The returned observable emits the expired items (not changesets). Subscribe to this observable to trigger the expiry mechanism; if not subscribed, no items will be removed.</para>
    /// </remarks>
    /// <seealso><c>LimitSizeTo&lt;T&gt;(ISourceList&lt;T&gt;, int, IScheduler?)</c></seealso>
    /// <seealso><c>ToObservableChangeSet&lt;T&gt;(IObservable&lt;T&gt;, Func&lt;T, TimeSpan?&gt;, IScheduler?)</c></seealso>
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
}
