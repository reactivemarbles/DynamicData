// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
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
}
