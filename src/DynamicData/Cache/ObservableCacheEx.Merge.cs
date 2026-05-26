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
/// ObservableCache extensions for MergeMany, MergeChangeSets, MergeManyChangeSets, MergeManyItems, and Switch.
/// </summary>
public static partial class ObservableCacheEx
{
    private const bool DefaultResortOnSourceRefresh = true;

    /// <summary>
    /// Subscribes to a child observable for each item in the source cache changeset stream and merges all child
    /// emissions into a single <see cref="IObservable{T}"/>. When an item is added, <paramref name="observableSelector"/>
    /// creates its child subscription. When updated, the previous child subscription is disposed and a new one is created.
    /// When removed, its child subscription is disposed. Refresh changes have no effect on subscriptions.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by child observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that produces a child observable for each source item.</param>
    /// <returns>An observable that emits values from all active child observables, interleaved by arrival order.</returns>
    /// <remarks>
    /// <para>
    /// This operator does not produce changesets. It produces a flat stream of <typeparamref name="TDestination"/>
    /// values, similar to Rx <c>SelectMany</c> but lifecycle-aware: child subscriptions track items entering and
    /// leaving the source cache.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="observableSelector"/> to create a child observable and subscribes to it. Emissions from the child flow into the merged output.</description></item>
    /// <item><term>Update</term><description>Disposes the previous child subscription and creates a new one for the updated item.</description></item>
    /// <item><term>Remove</term><description>Disposes the child subscription for the removed item.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The child observable continues unchanged.</description></item>
    /// <item><term>OnError</term><description>Errors from child observables are silently swallowed (the child is unsubscribed). Errors from the source changeset stream terminate the merged output.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The output is a plain <see cref="IObservable{TDestination}"/>, not a changeset stream. If you need merged changesets, use <see cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IComparer{TDestination}, IEqualityComparer{TDestination})"/> instead.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    /// <seealso cref="MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IComparer{TDestination}, IEqualityComparer{TDestination})"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <seealso cref="SubscribeMany{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IDisposable})"/>
    /// <seealso cref="ObservableListEx.MergeMany"/>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <inheritdoc cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives both the item and its key, and returns a child observable.</param>
    public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

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

    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. This overload requires a comparer for resolving destination key conflicts.
    /// The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observableSelector"/> or <paramref name="comparer"/> is null.</exception>
    /// <seealso cref="ObservableListEx.MergeManyChangeSets"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), comparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. This overload requires a comparer for resolving destination key conflicts.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeManyChangeSets(observableSelector, equalityComparer: null, comparer: comparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value for a destination key.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), equalityComparer, comparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child changeset stream and merges all child
    /// changes into a single flattened output stream. Child subscriptions track the parent item lifecycle:
    /// created on Add, replaced on Update, disposed on Remove.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source (parent) cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying parent items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a parent item and its key, and returns a child cache changeset stream. Called once per parent Add/Update.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress no-op child updates. When a child key's new value equals the current value per this comparer, the update is not emitted.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve child key conflicts when multiple parents contribute children with the same destination key. The lowest-ordered child value wins. Without a comparer, the first parent to provide a key retains priority.</param>
    /// <returns>A merged changeset stream containing all child items from all active parent subscriptions.</returns>
    /// <remarks>
    /// <para>
    /// This is the changeset-aware counterpart to <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>.
    /// Where MergeMany produces a flat <c>IObservable&lt;T&gt;</c>, MergeManyChangeSets produces an <c>IObservable&lt;IChangeSet&gt;</c>
    /// that tracks the full lifecycle of child items, including key conflict resolution across parents.
    /// </para>
    /// <para>
    /// <b>Parent-side change handling (source changeset events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="observableSelector"/> with the new parent item to obtain a child changeset stream, then subscribes. As the child stream emits changesets, those child items are merged into the output. The downstream observer sees <b>Add</b> changes for each new child item.</description></item>
    /// <item><term>Update</term><description>Disposes the previous parent's child subscription (removing all of its contributed child items from the output as <b>Remove</b> changes), then creates a new child subscription for the updated parent. The new child's items appear as <b>Add</b> changes.</description></item>
    /// <item><term>Remove</term><description>Disposes the parent's child subscription. All child items contributed by that parent are emitted as <b>Remove</b> changes in the output. If another parent also provides a child with the same destination key, that parent's value is promoted as an <b>Update</b> (not an Add).</description></item>
    /// <item><term>Refresh</term><description>No effect on the child subscription. The parent's child stream continues unchanged.</description></item>
    /// </list>
    /// <para>
    /// <b>Child-side change handling (changes arriving from child changeset streams):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the destination key is new, an <b>Add</b> is emitted. If another parent already contributed a child with the same key, the conflict is resolved by <paramref name="comparer"/> (lowest wins) or first-in-wins if no comparer. The losing value is tracked internally but not emitted.</description></item>
    /// <item><term>Update</term><description>If this parent currently owns the destination key downstream, an <b>Update</b> is emitted. With a comparer, all parents are re-evaluated for that key; a different parent's value may win, producing an <b>Update</b> to that value instead.</description></item>
    /// <item><term>Remove</term><description>If this parent's value was the one published downstream for that destination key, the operator scans other parents for the same key. If found, an <b>Update</b> is emitted with the replacement. If not, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the child item is the one currently published downstream, the <b>Refresh</b> is forwarded. With a comparer, all parents are re-evaluated first; if a different value now wins, an <b>Update</b> is emitted instead.</description></item>
    /// </list>
    /// <para>
    /// <b>Error and completion:</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>An error from the source (parent) stream or from any child changeset stream terminates the entire output. Unlike <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>, child errors are NOT swallowed.</description></item>
    /// <item><term>OnCompleted</term><description>The output completes when the source (parent) stream completes <b>and</b> all active child changeset streams have also completed.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> When multiple parents contribute children with the same destination key, only one value is published
    /// downstream at a time. The <paramref name="comparer"/> controls which value wins; without it, the first parent to add the key
    /// retains priority. Removing a parent that owned a contested key causes the next-best value (per comparer or next available)
    /// to surface as an <b>Update</b>, not an Add. The <paramref name="equalityComparer"/> independently controls whether a child
    /// Update for an already-published key is suppressed when the new value equals the old.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <seealso cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IObservable{IChangeSet{TDestination, TDestinationKey}}}, Func{TDestination, TDestinationKey})"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>(source, observableSelector, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/>.
    /// Uses <paramref name="sourceComparer"/> to resolve destination key conflicts by source priority.
    /// The selector receives only the item, not its key.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, DefaultResortOnSourceRefresh, equalityComparer: null, childComparer);
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/>.
    /// Uses <paramref name="sourceComparer"/> to resolve destination key conflicts by source priority.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/> and
    /// explicit <paramref name="resortOnSourceRefresh"/> control. The selector receives only the item.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, resortOnSourceRefresh, equalityComparer: null, childComparer);
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/> and
    /// explicit <paramref name="resortOnSourceRefresh"/> control.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, resortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets. Uses <paramref name="sourceComparer"/> to resolve
    /// destination key conflicts. The selector receives only the item, not its key.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, DefaultResortOnSourceRefresh, equalityComparer, childComparer);
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets. Uses <paramref name="sourceComparer"/> to resolve
    /// destination key conflicts. Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with full control over all conflict resolution parameters.
    /// The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, resortOnSourceRefresh, equalityComparer, childComparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child
    /// changes into a single flattened output. When multiple source items produce children with the same destination key,
    /// <paramref name="sourceComparer"/> determines which source has priority (the source ordering lower wins).
    /// If sources compare equal, <paramref name="childComparer"/> (if provided) breaks the tie.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/> (default), a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value for a destination key.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream containing items from all active child streams, with conflicts resolved by source priority.</returns>
    /// <remarks>
    /// <para>
    /// The <paramref name="sourceComparer"/> provides a layer of conflict resolution above the child values themselves.
    /// This is useful when source items represent priority tiers (e.g., user settings overriding defaults).
    /// </para>
    /// <para>
    /// Errors from child streams propagate to the output. An error from the source or any child terminates the merged output.
    /// The output completes when the source completes and all active child streams have also completed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="sourceComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        sourceComparer.ThrowArgumentNullExceptionIfNull(nameof(sourceComparer));

        return new MergeManyCacheChangeSetsSourceCompare<TObject, TKey, TDestination, TDestinationKey>(source, observableSelector, sourceComparer, equalityComparer, childComparer, resortOnSourceRefresh).Run();
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child list changeset stream produced by
    /// <paramref name="observableSelector"/> and merges all child changes into a single flattened list changeset output.
    /// Child subscriptions follow the source item lifecycle: created on Add, replaced on Update, disposed on Remove.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child list changeset streams.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child list changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to detect duplicate items in the merged list output.</param>
    /// <returns>A merged list changeset stream containing items from all active child streams.</returns>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyListChangeSets<TObject, TKey, TDestination>(source, observableSelector, equalityComparer).Run();
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child list changeset stream and merges all child changes
    /// into a single flattened list changeset output. The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child list changeset streams.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child list changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to detect duplicate items in the merged list output.</param>
    /// <returns>A merged list changeset stream containing items from all active child streams.</returns>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        return source.MergeManyChangeSets((obj, _) => observableSelector(obj), equalityComparer);
    }

    /// <summary>
    /// Like <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>,
    /// but wraps each emitted value as an <see cref="ItemWithValue{TObject, TValue}"/>, pairing the source item
    /// with the value it produced. This lets you identify which source item is responsible for each emission.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by child observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that produces a child observable for each source item.</param>
    /// <returns>An observable of <see cref="ItemWithValue{TObject, TValue}"/> pairing each emission with its source item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <inheritdoc cref="MergeManyItems{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives both the item and its key, and returns a child observable.</param>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <inheritdoc cref="Switch{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <param name="sources">An observable that emits <see cref="IObservableCache{TObject, TKey}"/> instances.</param>
    /// <remarks>Overload that accepts observable caches. Internally calls <c>Connect()</c> on each cache and delegates to the changeset overload.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Subscribes to the latest inner changeset stream, unsubscribing from the previous one on each switch.
    /// When switching, the old source's items are removed and the new source's items are added.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">An <see cref="IObservable{T}"/> of <see cref="IObservable{T}"/> changeset streams. The operator subscribes to the latest inner stream.</param>
    /// <returns>A changeset stream reflecting the items from the most recently emitted inner source.</returns>
    /// <remarks>
    /// <para>On switch: <b>Remove</b> is emitted for all items from the previous source, then <b>Add</b> for all items from the new source.</para>
    /// <para><b>Worth noting:</b> Each switch clears the entire downstream cache before populating from the new source. Subscribers see a full remove-then-add reset on every switch.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Switch<TObject, TKey>(sources).Run();
    }
}
