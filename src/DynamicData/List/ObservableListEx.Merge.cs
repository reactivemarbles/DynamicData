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
/// ObservableList extensions for MergeMany, MergeChangeSets, MergeManyChangeSets, and Switch.
/// </summary>
public static partial class ObservableListEx
{
    /// <inheritdoc cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <summary>
    /// Merges multiple list changeset streams from an observable-of-observables into a single unified changeset stream.
    /// Unlike <see cref="ObservableCacheEx.MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}}, IEqualityComparer{TObject})"/>, list merging performs no key-based deduplication.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> of nested changeset observables.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used by the merge tracker to compare items.</param>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject>(source, equalityComparer).Run();
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <summary>
    /// Merges two list changeset streams into a single unified stream.
    /// </summary>
    /// <param name="source">The first <see cref="IObservable{IChangeSet{TObject}}"/> to merge.</param>
    /// <param name="other">The second <see cref="IObservable{IChangeSet{TObject}}"/> to merge with.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used to compare items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IObservable<IChangeSet<TObject>> other, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));

        return new[] { source, other }.MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <summary>
    /// Merges the source list changeset stream with additional changeset streams into a single unified stream.
    /// </summary>
    /// <param name="source">The primary source <see cref="IObservable{IChangeSet{TObject}}"/> to merge.</param>
    /// <param name="others">The additional <see cref="IEnumerable{T}"/> of list changeset streams to merge with.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used to compare items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IEnumerable<IObservable<IChangeSet<TObject>>> others, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Merges a collection of list changeset streams into a single unified changeset stream.
    /// This is the canonical list MergeChangeSets overload: other overloads accepting <see cref="IObservable{T}"/>, <see cref="IObservableList{T}"/>, or pair/params variants ultimately produce equivalent behavior.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The <see cref="IEnumerable{T}"/> collection of list changeset streams to merge.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> used by the merge tracker to compare items. Defaults to <see cref="EqualityComparer{T}.Default"/> when <see langword="null"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    /// <returns>A single list changeset stream containing all changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All changes from inner streams are forwarded to the output. There is no key-based deduplication (unlike <see cref="ObservableCacheEx.MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}}, IEqualityComparer{TObject})"/>): if the same item appears in multiple inner streams, it will appear multiple times in the merged output.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Replace</b></term><description>The old value is replaced by the new value in the merged output. If the old value is not found (by <paramref name="equalityComparer"/>), the new value is added instead.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="MergeChangeSets{TObject}(IObservable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="Or{T}(IObservable{IChangeSet{T}}, IObservable{IChangeSet{T}}[])"/>
    /// <seealso cref="ObservableCacheEx.MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}}, IEqualityComparer{TObject})"/>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IEnumerable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject>(source, equalityComparer, completable, scheduler).Run();
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IObservable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?)"/>
    /// <summary>
    /// Merges list changeset streams from an <see cref="IObservableList{T}"/> into a single stream. Sources can be added or removed dynamically.
    /// </summary>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservableList<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(equalityComparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject}(IObservable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?)"/>
    /// <summary>
    /// Merges list changeset streams from a list-of-list-changeset-observables into a single stream.
    /// Each inner list changeset observable in the source list is merged, and parent item removal triggers child cleanup.
    /// </summary>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject>>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeManyChangeSets(static src => src, equalityComparer);
    }

    /// <summary>
    /// Merges cache changeset streams from an <see cref="IObservableList{T}"/> into a single cache changeset stream.
    /// Uses <paramref name="comparer"/> to resolve conflicts when the same key appears in multiple child streams.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TKey">The type of the object key.</typeparam>
    /// <param name="source">The <see cref="IObservableList{T}"/> of cache changeset observables.</param>
    /// <param name="comparer"><see cref="IComparer{TObject}"/> to resolve which value wins when the same key appears in multiple sources.</param>
    /// <returns>A single cache changeset stream with key-based deduplication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Sources can be added or removed dynamically from the observable list. Parent item removal triggers cleanup of all child items from that source.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b> (child)</term><description>If the destination key is new, an <b>Add</b> is emitted. If another source already contributed a child with the same key, <paramref name="comparer"/> resolves the conflict (lowest-ordered value wins). The losing value is tracked internally but not emitted.</description></item>
    /// <item><term><b>Update</b> (child)</term><description>If this source currently owns the destination key downstream, an <b>Update</b> is emitted. Otherwise <paramref name="comparer"/> re-evaluates all sources; a different source's value may win, producing an <b>Update</b> to that value instead.</description></item>
    /// <item><term><b>Remove</b> (child)</term><description>If this source's value was the one published downstream for that destination key, the operator scans other sources for the same key. If found, an <b>Update</b> is emitted with the replacement (per <paramref name="comparer"/>). Otherwise a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Refresh</b> (child)</term><description>If the child item is the one currently published downstream, the <b>Refresh</b> is forwarded. Otherwise <paramref name="comparer"/> re-evaluates all sources; if a different value now wins, an <b>Update</b> is emitted instead.</description></item>
    /// <item><term>Source list <b>Add</b></term><description>Subscribes to the new child changeset stream and merges its keys into the output.</description></item>
    /// <item><term>Source list <b>Remove</b></term><description>Disposes that source's subscription. All keys it contributed are removed. For keys also contributed by other sources, the next-best value (per <paramref name="comparer"/>) is promoted as an <b>Update</b>, not an Add.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IEqualityComparer{TObject}?, IComparer{TObject}?)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(comparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    /// <summary>
    /// Merges cache changeset streams from an <see cref="IObservableList{T}"/> into a single cache changeset stream, with optional equality and ordering comparers.
    /// </summary>
    /// <param name="source">The <see cref="IObservableList{T}"/> of cache changeset observables.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <see cref="IComparer{TObject}"/> to resolve conflicts when the same key appears in multiple sources.</param>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(equalityComparer, comparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    /// <summary>
    /// Merges cache changeset streams from a list changeset of cache changeset observables, using a comparer for conflict resolution.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> whose items are cache changeset observables.</param>
    /// <param name="comparer"><see cref="IComparer{TObject}"/> to resolve which value wins when the same key appears in multiple sources.</param>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeChangeSets(comparer);
    }

    /// <inheritdoc cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    /// <summary>
    /// Merges cache changeset streams from a list changeset of cache changeset observables, with optional equality and ordering comparers.
    /// </summary>
    /// <param name="source">The source <see cref="IObservable{T}"/> whose items are cache changeset observables.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <see cref="IComparer{TObject}"/> to resolve conflicts when the same key appears in multiple sources.</param>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeManyChangeSets(static src => src, equalityComparer, comparer);
    }

    /// <summary>
    /// Subscribes to a per-item observable for each item in the source and merges all emissions into a single <see cref="IObservable{TDestination}"/> stream.
    /// This is NOT a changeset operator: it returns a flat observable of values.
    /// </summary>
    /// <typeparam name="T">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by per-item observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns an observable for each source item.</param>
    /// <returns>An observable that emits values from all per-item observables, merged together.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Subscription behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the per-item observable. Emissions are merged into the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old subscription disposed, new subscription created for the replacement item.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Subscription disposed.</description></item>
    /// <item><term><b>Refresh</b>/<b>Moved</b></term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnCompleted (source)</term><description>Completes only after the source and all active inner observables have completed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="SubscribeMany{T}(IObservable{IChangeSet{T}}, Func{T, IDisposable})"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="WhenPropertyChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="ObservableCacheEx.MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    public static IObservable<TDestination> MergeMany<T, TDestination>(this IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<T, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Transforms each source item into a child list changeset stream using <paramref name="observableSelector"/>,
    /// then merges all child streams into a single flat list changeset stream. Parent item removal cleans up all associated children.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns a child list changeset stream for each source item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TDestination}"/> used to compare child items.</param>
    /// <returns>A single list changeset stream containing all items from all child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Internally subscribes to each child stream when a source item is added and disposes the subscription when it is removed.
    /// All child items from a removed parent are removed from the merged output.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the child stream. Child emissions are merged into the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old child subscription disposed (and its items removed from output). New child subscription created.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Child subscription disposed. All child items from that parent are removed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="MergeChangeSets{TObject}(IEnumerable{IObservable{IChangeSet{TObject}}}, IEqualityComparer{TObject}?, IScheduler?, bool)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <seealso cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="ObservableCacheEx.MergeManyChangeSets{TObject, TKey, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}, IComparer{TDestination})"/>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TDestination : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (observableSelector == null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        return new MergeManyListChangeSets<TObject, TDestination>(source, observableSelector, equalityComparer).Run();
    }

    /// <summary>
    /// Transforms each source item into a child cache changeset stream and merges all children into a single cache changeset stream.
    /// Uses <paramref name="comparer"/> to resolve key conflicts when the same key appears in multiple child streams.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child cache changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key in the child cache changesets.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns a child cache changeset stream for each source item.</param>
    /// <param name="comparer"><see cref="IComparer{TDestination}"/> to resolve which value wins when the same key appears from multiple children.</param>
    /// <returns>A single cache changeset stream with key-based deduplication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="comparer"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <inheritdoc cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <para>Delegates to <see cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/> with a <see langword="null"/> equality comparer.</para>
    /// </remarks>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination, TDestinationKey}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination, TDestinationKey}}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeManyChangeSets(observableSelector, equalityComparer: null, comparer: comparer);
    }

    /// <summary>
    /// Transforms each source item into a child cache changeset stream and merges all children into a single cache changeset stream.
    /// This is the primary list-to-cache MergeManyChangeSets overload.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child cache changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key in the child cache changesets.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> function that returns a child cache changeset stream for each source item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TDestination}"/> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <see cref="IComparer{TDestination}"/> to resolve conflicts when the same key appears from multiple children.</param>
    /// <returns>A single cache changeset stream with key-based deduplication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Each source item produces a keyed child stream via <paramref name="observableSelector"/>. All child items are tracked by key.
    /// When a parent item is removed, all its child items are removed from the merged output.
    /// When the same key appears from multiple children, <paramref name="comparer"/> determines which value wins.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the child cache stream. Child key/value pairs are merged into the output cache.</description></item>
    /// <item><term><b>Replace</b></term><description>Old child subscription disposed (and its keys removed from output). New child subscription created.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Child subscription disposed. All keys originating from that child are removed from the output.</description></item>
    /// <item><term><b>Moved</b>/<b>Refresh</b></term><description>Ignored; this operator emits a cache changeset and source ordering/refresh does not affect key membership.</description></item>
    /// </list>
    /// <para>
    /// <b>Error and completion:</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>An error from the source (parent) stream or from any child changeset stream terminates the entire output. Unlike <see cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>, child errors are NOT swallowed.</description></item>
    /// <item><term>OnCompleted</term><description>The output completes when the source (parent) stream completes <b>and</b> all active child changeset streams have also completed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservableList{IObservable{IChangeSet{TObject, TKey}}}, IComparer{TObject})"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>(source, observableSelector, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Subscribes to the latest inner <see cref="IObservableList{T}"/>, switching to each new source and clearing the result when switching.
    /// This is the changeset-aware equivalent of Rx's <see cref="Observable.Switch{TSource}(IObservable{IObservable{TSource}})"/>, which cannot be applied directly to changeset streams.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An observable that emits <see cref="IObservableList{T}"/> instances. Each emission triggers a switch to the new list.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Convenience overload that calls <c>Connect()</c> on each inner list, then delegates to <see cref="Switch{T}(IObservable{IObservable{IChangeSet{T}}})"/>.</para>
    /// </remarks>
    /// <seealso cref="Switch{T}(IObservable{IObservable{IChangeSet{T}}})"/>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservableList<T>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Subscribes to the latest inner changeset stream, switching to each new source and clearing the destination when switching.
    /// Previous subscriptions are disposed and the result set is emptied before subscribing to the new inner stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">An <see cref="IObservable{T}"/> of <see cref="IObservable{T}"/> changeset streams. The operator subscribes to the latest inner stream.</param>
    /// <returns>A list changeset stream reflecting the most recently received inner changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// On each new inner stream, the operator clears the destination, disposes the previous subscription, and subscribes to the new stream.
    /// This is the changeset-aware equivalent of Rx's <c>Switch()</c>.
    /// </para>
    /// </remarks>
    /// <seealso cref="Switch{T}(IObservable{IObservableList{T}})"/>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservable<IChangeSet<T>>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Switch<T>(sources).Run();
    }
}
