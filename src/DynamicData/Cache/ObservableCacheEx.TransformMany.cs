// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

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
    /// Flattens each source item into zero or more destination items (1:N), producing a single flat changeset.
    /// Each child item must have a globally unique key across all parents.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;</c> to expand each item into multiple children.</param>
    /// <param name="manySelector">A function that expands a parent item into its children. For <c>ObservableCollection&lt;T&gt;</c> or <c>IObservableCache&lt;TObject, TKey&gt;</c> overloads, subsequent changes to the child collection are automatically tracked.</param>
    /// <param name="keySelector">A <c>Func&lt;T, TResult&gt;</c> that extracts a unique key from each child item. Keys must be unique across ALL parents, not just within one parent.</param>
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
    /// <para>If two parents produce children with the same key, last-in-wins. Use the async variant with a <c>IComparer&lt;T&gt;</c> to control conflict resolution.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <seealso><c>TransformManyAsync&lt;TDestination, TDestinationKey, TSource, TSourceKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;, Func&lt;TSource, TSourceKey, Task&lt;IEnumerable&lt;TDestination&gt;&gt;&gt;, Func&lt;TDestination, TDestinationKey&gt;, IEqualityComparer&lt;TDestination&gt;?, IComparer&lt;TDestination&gt;?)</c></seealso>
    /// <seealso><c>ObservableListEx.TransformMany</c></seealso>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Provides an overload of <c>Run</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts an <c>ObservableCollection&lt;T&gt;</c> selector. Changes to the child collection (adds, removes, replacements) are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Provides an overload of <c>Run</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts a <c>ReadOnlyObservableCollection&lt;T&gt;</c> selector. Changes to the child collection are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Provides an overload of <c>Run</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts an <c>IObservableCache&lt;TObject, TKey&gt;</c> selector. The child cache is live: subsequent changes to it are automatically propagated downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IObservableCache<TDestination, TDestinationKey>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();
}
