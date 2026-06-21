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
    /// Bridges an <c>IObservable&lt;T&gt;</c> into the DynamicData world by converting each emitted item into a list changeset.
    /// Each emission becomes an <b>Add</b> operation in the resulting changeset stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> to convert into a changeset stream.</param>
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
    /// <seealso><c>ToObservableChangeSet&lt;T&gt;(IObservable&lt;T&gt;, Func&lt;T, TimeSpan?&gt;, int, IScheduler?)</c></seealso>
    /// <seealso><c>ToObservableChangeSet&lt;T&gt;(IObservable&lt;IEnumerable&lt;T&gt;&gt;, IScheduler?)</c></seealso>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<T> source,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Bridges an <c>IObservable&lt;T&gt;</c> into a list changeset stream with per-item time-based expiry.
    /// Expired items are automatically removed.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <c>Func&lt;T, TResult&gt;</c> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers.</param>
    /// <returns>The resulting observable sequence.</returns>
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

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Bridges an <c>IObservable&lt;T&gt;</c> into a list changeset stream with FIFO size limiting.
    /// When the list exceeds <paramref name="limitSizeTo"/>, the oldest items are removed.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> to convert into a changeset stream.</param>
    /// <param name="limitSizeTo">The maximum list size. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling removals.</param>
    /// <returns>The resulting observable sequence.</returns>
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

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Bridges an <c>IObservable&lt;T&gt;</c> into a list changeset stream with both time-based expiry and FIFO size limiting.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <c>Func&lt;T, TResult&gt;</c> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="limitSizeTo">The maximum list size. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers and size-limit checks.</param>
    /// <returns>The resulting observable sequence.</returns>
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

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Bridges an <c>IObservable&lt;T&gt;</c> of <c>IEnumerable&lt;T&gt;</c> batches into a list changeset stream.
    /// Each emitted batch becomes an <b>AddRange</b>.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IEnumerable&lt;T&gt;&gt;</c> of <c>IEnumerable&lt;T&gt;</c> to convert into a changeset stream.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for time-based operations.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(
                this IObservable<IEnumerable<T>> source,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ToObservableChangeSet<T>.Create(
            source: source,
            expireAfter: null,
            limitSizeTo: -1,
            scheduler: scheduler);

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Bridges an <c>IObservable&lt;T&gt;</c> of <c>IEnumerable&lt;T&gt;</c> batches into a list changeset stream with FIFO size limiting.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IEnumerable&lt;T&gt;&gt;</c> of <c>IEnumerable&lt;T&gt;</c> to convert into a changeset stream.</param>
    /// <param name="limitSizeTo">The maximum list size. Oldest items are removed when the limit is exceeded.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling removals.</param>
    /// <returns>The resulting observable sequence.</returns>
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

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Bridges an <c>IObservable&lt;T&gt;</c> of <c>IEnumerable&lt;T&gt;</c> batches into a list changeset stream with time-based expiry.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IEnumerable&lt;T&gt;&gt;</c> of <c>IEnumerable&lt;T&gt;</c> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <c>Func&lt;T, TResult&gt;</c> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers.</param>
    /// <returns>The resulting observable sequence.</returns>
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

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Bridges an <c>IObservable&lt;T&gt;</c> of <c>IEnumerable&lt;T&gt;</c> batches into a list changeset stream with both time-based expiry and FIFO size limiting.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IEnumerable&lt;T&gt;&gt;</c> of <c>IEnumerable&lt;T&gt;</c> to convert into a changeset stream.</param>
    /// <param name="expireAfter">A <c>Func&lt;T, TResult&gt;</c> function returning the time-to-live for each item. Return <see langword="null"/> for non-expiring items.</param>
    /// <param name="limitSizeTo">The maximum list size. Oldest items removed when exceeded.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for expiry timers and size-limit checks.</param>
    /// <returns>The resulting observable sequence.</returns>
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
}
