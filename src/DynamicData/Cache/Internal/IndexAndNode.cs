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