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
    /// Flattens each source item into multiple destination items using <paramref name="manySelector"/>. Each source item produces zero or more children,
    /// all of which are merged into a single flat list changeset stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> to expand each item into multiple children.</param>
    /// <param name="manySelector">A <see cref="Func{T, TResult}"/> function that returns the child items for each source item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TDestination}"/> used during Replace to determine which child items changed between old and new parent values.</param>
    /// <returns>A list changeset stream of all child items from all source items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="manySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Children expanded and added to the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old children diffed against new children (using <paramref name="equalityComparer"/>). Removed, added, or kept as appropriate.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>All children of the removed parents are removed from the output.</description></item>
    /// <item><term><b>Refresh</b></term><description>Children re-expanded and diffed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="ObservableCacheEx.TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IEnumerable<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
    }

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from an <see cref="ObservableCollection{T}"/>. The collection is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from a <see cref="ReadOnlyObservableCollection{T}"/>. The collection is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from an <see cref="IObservableList{T}"/>. The inner list is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IObservableList<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
}
