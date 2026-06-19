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
    /// Flattens each source item into zero or more destination items (1:N), producing a single flat changeset.
    /// Each child item must have a globally unique key across all parents.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TSourceKey}}"/> to expand each item into multiple children.</param>
    /// <param name="manySelector">A function that expands a parent item into its children. For <see cref="ObservableCollection{T}"/> or <see cref="IObservableCache{TObject, TKey}"/> overloads, subsequent changes to the child collection are automatically tracked.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that extracts a unique key from each child item. Keys must be unique across ALL parents, not just within one parent.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="manySelector"/>, emits Add for each child.</description></item>
    ///   <item><term>Update</term><description>Diffs old children vs new children: emits Remove for removed children, Add for new children, Update for children with matching keys.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove for all children of the removed parent.</description></item>
    ///   <item><term>Refresh</term><description>Propagated as Refresh to all children (no re-expansion).</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> If two source items produce children with the same key, last-in-wins. <b>Refresh</b> does NOT re-expand children (only <b>Update</b> does).</para>
    /// <para>If two parents produce children with the same key, last-in-wins. Use the async variant with a <see cref="IComparer{T}"/> to control conflict resolution.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <seealso cref="ObservableListEx.TransformMany"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="ObservableCollection{T}"/> selector. Changes to the child collection (adds, removes, replacements) are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts a <see cref="ReadOnlyObservableCollection{T}"/> selector. Changes to the child collection are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="IObservableCache{TObject, TKey}"/> selector. The child cache is live: subsequent changes to it are automatically propagated downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IObservableCache<TDestination, TDestinationKey>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();
}
