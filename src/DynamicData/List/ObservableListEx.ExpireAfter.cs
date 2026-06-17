// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

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
}
