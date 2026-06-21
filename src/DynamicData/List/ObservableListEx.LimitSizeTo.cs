// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
#endif

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
    /// Limits the source list to a maximum number of items using FIFO eviction.
    /// When the list exceeds <paramref name="sizeLimit"/>, the oldest items are removed.
    /// Returns an observable of the items that were removed.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <c>ISourceList&lt;T&gt;</c> source list to apply size limits to.</param>
    /// <param name="sizeLimit">The maximum number of items allowed. Must be greater than zero.</param>
    /// <param name="scheduler">The scheduler for scheduling size checks. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits collections of items each time excess items are removed from the source list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sizeLimit"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// This operator acts directly on an <c>ISourceList&lt;T&gt;</c>. It subscribes to the source's changes,
    /// tracks insertion order using an internal Transform, and removes the oldest items when the size limit is exceeded.
    /// </para>
    /// <para><b>Worth noting:</b> The returned observable emits the removed items (not changesets). Subscribe to this observable to activate the size-limiting mechanism. Removal is performed synchronously under a lock shared with the change tracking.</para>
    /// </remarks>
    /// <seealso><c>ExpireAfter&lt;T&gt;(ISourceList&lt;T&gt;, Func&lt;T, TimeSpan?&gt;, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>Top&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, int)</c></seealso>
    public static IObservable<IEnumerable<T>> LimitSizeTo<T>(this ISourceList<T> source, int sizeLimit, IScheduler? scheduler = null)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (sizeLimit <= 0)
        {
            throw new ArgumentException("sizeLimit cannot be zero", nameof(sizeLimit));
        }

        var locker = InternalEx.NewLock();
        var limiter = new LimitSizeTo<T>(source, sizeLimit, scheduler ?? GlobalConfig.DefaultScheduler, locker);

        return limiter.Run().Synchronize(locker).Do(source.RemoveMany);
    }
}
