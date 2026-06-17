// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
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
