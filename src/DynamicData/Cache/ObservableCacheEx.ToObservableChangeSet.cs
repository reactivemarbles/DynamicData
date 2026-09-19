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
/// Extensions for dynamic data.
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
    /// <param name="source">The source <c>IObservable&lt;TObject&gt;</c> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">A <c>Func&lt;T, TResult&gt;</c> that selects the unique key for each item.</param>
    /// <param name="expireAfter">An optional <c>Func&lt;T, TResult&gt;</c> that specifies per-item expiration time. Return <see langword="null"/> for no expiration.</param>
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
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);

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
    /// <param name="source">The source <c>IObservable&lt;IEnumerable&lt;TObject&gt;&gt;</c> to convert into a keyed changeset stream.</param>
    /// <param name="keySelector">A <c>Func&lt;T, TResult&gt;</c> that selects the unique key for each item.</param>
    /// <param name="expireAfter">An optional <c>Func&lt;T, TResult&gt;</c> that specifies per-item expiration time. Return <see langword="null"/> for no expiration.</param>
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
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(keySelector);

        return Cache.Internal.ToObservableChangeSet<TObject, TKey>.Create(
            source: source,
            keySelector: keySelector,
            expireAfter: expireAfter,
            limitSizeTo: limitSizeTo,
            scheduler: scheduler);
    }
}
