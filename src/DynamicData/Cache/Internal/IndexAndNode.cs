// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the IndexAndNode class.
/// </summary>
internal static class IndexAndNode
{
    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <typeparam name="TNodeValue">The type of the TNodeValue value.</typeparam>
    /// <param name="index">The index value.</param>
    /// <param name="value">The value value.</param>
    /// <returns>The result of the operation.</returns>
    public static IndexAndNode<TNodeValue> Create<TNodeValue>(int index, LinkedListNode<TNodeValue> value) => new(index, value);
}

/// <summary>
/// Provides members for the IndexAndNode class.
/// </summary>
/// <typeparam name="TNodeValue">The type of the TNodeValue value.</typeparam>
/// <param name="index">The index value.</param>
/// <param name="node">The node value.</param>
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, different generics.")]
internal sealed class IndexAndNode<TNodeValue>(int index, LinkedListNode<TNodeValue> node)
{
    /// <summary>
    /// Gets the Index value.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    /// Gets the Node value.
    /// </summary>
    public LinkedListNode<TNodeValue> Node { get; } = node;
}
