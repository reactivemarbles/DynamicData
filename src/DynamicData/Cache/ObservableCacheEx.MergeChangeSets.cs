// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Merges multiple changeset streams that arrive dynamically into a single unified changeset stream.
    /// Each inner stream emitted by the outer observable is subscribed and its changes forwarded downstream.
    /// When multiple sources provide the same key, the first source to add it retains priority unless a
    /// comparer-based overload is used.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <remarks>
    /// <para>
    /// Each inner changeset stream is independently tracked in its own cache. When multiple sources provide the same key,
    /// this overload uses first-in-wins semantics: the value from whichever source added the key first is
    /// the one published downstream. To control which value wins for duplicate keys, use an overload that
    /// accepts an <see cref="IComparer{T}"/>, which selects the lowest-ordered value across all sources.
    /// An <see cref="IEqualityComparer{T}"/> can be provided separately to suppress no-op updates when
    /// the new value equals the currently published value for a key.
    /// </para>
    /// <para>
    /// <b>Overload families:</b> MergeChangeSets has 16 overloads organized along three axes:
    /// (1) <b>Source type</b>: dynamic (<c>IObservable&lt;IObservable&lt;IChangeSet&gt;&gt;</c>, sources arrive at runtime),
    /// pair (<c>source + other</c>, exactly two streams), or static (<see cref="IEnumerable{T}"/>, all sources known up front).
    /// (2) <b>Conflict resolution</b>: none (first-in-wins), <see cref="IComparer{T}"/> (lowest-ordered wins),
    /// <see cref="IEqualityComparer{T}"/> (suppresses duplicate updates), or both.
    /// (3) <b>Completion</b>: static overloads accept a <c>completable</c> flag; when <see langword="false"/>, the output never completes
    /// even after all sources finish (useful for "live" merge scenarios).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If no source has previously provided this key, an <b>Add</b> is emitted downstream. If another source already holds this key, the new value is tracked internally but not emitted (first-in-wins). With a comparer, the lowest-ordered value across all sources is selected and published instead.</description></item>
    /// <item><term>Update</term><description>If the updating source currently owns the downstream value for this key, an <b>Update</b> is emitted. If a comparer is provided and the update causes a different source's value to become the best candidate, an <b>Update</b> is emitted with that other source's value.</description></item>
    /// <item><term>Remove</term><description>If the removed value was the one published downstream, the operator scans all remaining sources for the same key. If another source still holds that key, an <b>Update</b> is emitted with the replacement value (selected by comparer if provided, otherwise the next available). If no other source holds the key, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the refreshed item matches the currently published value, the <b>Refresh</b> is forwarded. With a comparer, all sources are re-evaluated first; if a different value now wins, an <b>Update</b> is emitted instead of the Refresh.</description></item>
    /// <item><term>OnCompleted</term><description>For dynamic overloads, the output completes when the outer observable completes and all subscribed inner observables have also completed. For static overloads, completion depends on the <c>completable</c> parameter (default <see langword="true"/>).</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> When a source removes a key that was published downstream, the fallback to another
    /// source's value is emitted as an <b>Update</b> (not an Add). This can be surprising if you expect
    /// a Remove followed by an Add. Also, errors from any single inner source terminate the entire merged
    /// stream, so consider error handling within individual sources if isolation is needed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IComparer{TDestination}, IEqualityComparer{TDestination})"/>
    /// <seealso cref="ObservableListEx.MergeChangeSets"/>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer: null).Run();
    }

    /// <summary>
    /// Merges dynamic cache changeset streams into a single output, using a comparer to resolve key conflicts.
    /// When multiple sources provide the same key, the item ordering lowest according to <paramref name="comparer"/>
    /// is published downstream.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer).Run();
    }

    /// <summary>
    /// Merges dynamic cache changeset streams into a single output, using an equality comparer to suppress
    /// redundant updates. When an incoming value for a key is equal (per <paramref name="equalityComparer"/>)
    /// to the currently published value, the update is suppressed.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="equalityComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer: null).Run();
    }

    /// <summary>
    /// Merges dynamic cache changeset streams into a single output, using both a comparer for key conflict resolution
    /// and an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">An <see cref="IObservable{T}"/> that emits changeset streams. Each inner stream is subscribed as it appears.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <returns>A unified changeset stream containing changes from all active source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Convenience overload that merges exactly two cache changeset streams into a single output.
    /// Uses first-in-wins semantics for key conflicts.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="other">The second <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge with <paramref name="source"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="other"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));

        return new[] { source, other }.MergeChangeSets(scheduler, completable);
    }

    /// <summary>
    /// Convenience overload that merges exactly two cache changeset streams, using a comparer for key conflict resolution.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="other">The second <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge with <paramref name="source"/>.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when both sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="other"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new[] { source, other }.MergeChangeSets(comparer, scheduler, completable);
    }

    /// <summary>
    /// Convenience overload that merges exactly two cache changeset streams, using an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="other">The second <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="other"/>, or <paramref name="equalityComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IEqualityComparer<TObject> equalityComparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new[] { source, other }.MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Convenience overload that merges exactly two cache changeset streams, using both a comparer and an equality comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="other">The second <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when both sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when both streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from both sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="other"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<IChangeSet<TObject, TKey>> other, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new[] { source, other }.MergeChangeSets(equalityComparer, comparer, scheduler, completable);
    }

    /// <summary>
    /// Merges <paramref name="source"/> with additional changeset streams into a single output.
    /// Uses first-in-wins semantics for key conflicts.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to merge with <paramref name="source"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="others"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.EnumerateOne().Concat(others).MergeChangeSets(scheduler, completable);
    }

    /// <summary>
    /// Merges <paramref name="source"/> with additional changeset streams, using a comparer for key conflict resolution.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to merge with <paramref name="source"/>.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when multiple sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="others"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.EnumerateOne().Concat(others).MergeChangeSets(comparer, scheduler, completable);
    }

    /// <summary>
    /// Merges <paramref name="source"/> with additional changeset streams, using an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="others"/>, or <paramref name="equalityComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IEqualityComparer<TObject> equalityComparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Merges <paramref name="source"/> with additional changeset streams, using both a comparer and an equality comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to merge.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to merge with <paramref name="source"/>.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when multiple sources provide the same key.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all streams complete. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="others"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IEnumerable<IObservable<IChangeSet<TObject, TKey>>> others, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, comparer, scheduler, completable);
    }

    /// <summary>
    /// Merges a fixed collection of cache changeset streams into a single unified output. All source streams are
    /// subscribed when the output observable is subscribed to.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IEnumerable{T}"/> to merge.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <remarks>
    /// <para>
    /// When multiple sources provide items with the same key, this overload uses first-in-wins semantics:
    /// the first source to provide a key retains priority. Removing that source's item allows the next
    /// available value for that key (if any) to surface. To control which value wins, use an overload
    /// that accepts an <see cref="IComparer{T}"/>.
    /// </para>
    /// <para>
    /// An error from any source terminates the entire merged output.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer: null, completable, scheduler).Run();
    }

    /// <summary>
    /// Merges a fixed collection of cache changeset streams into a single output, using a comparer for key conflict
    /// resolution. When multiple sources provide the same key, the item ordering lowest according to
    /// <paramref name="comparer"/> is published downstream.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IEnumerable{T}"/> to merge.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer: null, comparer, completable, scheduler).Run();
    }

    /// <summary>
    /// Merges a fixed collection of cache changeset streams into a single output, using an equality comparer to
    /// suppress redundant updates. When an incoming value for a key is equal (per <paramref name="equalityComparer"/>)
    /// to the currently published value, the update is suppressed.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IEnumerable{T}"/> to merge.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="equalityComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer: null, completable, scheduler).Run();
    }

    /// <summary>
    /// Merges a fixed collection of cache changeset streams into a single output, using both a comparer for key
    /// conflict resolution and an equality comparer to suppress redundant updates.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the changesets.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying items.</typeparam>
    /// <param name="source">The source <see cref="IEnumerable{T}"/> to merge.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that equality comparer to detect duplicate values for the same key, suppressing no-op updates.</param>
    /// <param name="comparer">An <see cref="IComparer{TObject}"/> that comparer to determine which value wins when multiple sources provide the same key. The lowest-ordered value is published.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> used when subscribing to the source streams.</param>
    /// <param name="completable">If <see langword="true"/> (default), the output completes when all source streams have completed. If <see langword="false"/>, the output never completes.</param>
    /// <returns>A unified changeset stream containing changes from all source streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="equalityComparer"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject> equalityComparer, IComparer<TObject> comparer, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        equalityComparer.ThrowArgumentNullExceptionIfNull(nameof(equalityComparer));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new MergeChangeSets<TObject, TKey>(source, equalityComparer, comparer, completable, scheduler).Run();
    }
}
