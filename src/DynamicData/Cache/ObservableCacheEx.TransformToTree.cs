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
    /// Builds a hierarchical tree from a flat changeset using a parent key selector.
    /// Each item becomes a <c>Node&lt;TObject, TKey&gt;</c> with Parent, Children, Depth, and IsRoot properties.
    /// </summary>
    /// <typeparam name="TObject">The type of the source items. Must be a reference type.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to transform into a hierarchical tree.</param>
    /// <param name="pivotOn">The <c>Func&lt;TObject, TKey&gt;</c> that returns the key of an item's parent. Return the item's own key (or a non-existent key) for root items.</param>
    /// <param name="predicateChanged">An optional <c>IObservable&lt;T&gt;</c> that emits a filter predicate for nodes. When the predicate changes, nodes are re-evaluated and filtered.</param>
    /// <returns>An observable changeset of <c>Node&lt;TObject, TKey&gt;</c> items representing the tree.</returns>
    /// <remarks>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Creates node, attaches to parent (or root if parent not found), emits Add.</description></item>
    ///   <item><term>Update</term><description>Updates node. If <paramref name="pivotOn"/> returns a different parent key, the node is re-parented.</description></item>
    ///   <item><term>Remove</term><description>Removes node. Orphaned children become root nodes.</description></item>
    ///   <item><term>Refresh</term><description>Re-evaluates parent key. May re-parent the node if the parent changed.</description></item>
    /// </list>
    /// <para>Circular references are NOT detected. If item A is the parent of B and B is the parent of A, behavior is undefined.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="pivotOn"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> TransformToTree<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey> pivotOn, IObservable<Func<Node<TObject, TKey>, bool>>? predicateChanged = null)
        where TObject : class
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(pivotOn);

        return new TreeBuilder<TObject, TKey>(source, pivotOn, predicateChanged).Run();
    }
}
