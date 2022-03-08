// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Cache.Internal;

internal static class IndexAndNode
{
    public static IndexAndNode<TNodeValue> Create<TNodeValue>(int index, LinkedListNode<TNodeValue> value)
    {
        return new(index, value);
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, different generics.")]
internal class IndexAndNode<TNodeValue>
{
    public IndexAndNode(int index, LinkedListNode<TNodeValue> node)
    {
        Index = index;
        Node = node;
    }

    public int Index { get; }

    public LinkedListNode<TNodeValue> Node { get; }
}
