// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
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

    internal static class IndexAndNode
    {
        public static IndexAndNode<TNodeValue> Create<TNodeValue>(int index, LinkedListNode<TNodeValue> value)
        {
            return new IndexAndNode<TNodeValue>(index, value);
        }
    }
}