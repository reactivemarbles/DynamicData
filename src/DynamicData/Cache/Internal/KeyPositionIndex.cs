// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Maintains the list position of each key in an ordered projection, supporting insertion and removal at an
/// arbitrary position without rewriting the positions of the surviving entries.
/// </summary>
/// <remarks>
/// Positions are derived on demand from subtree sizes rather than stored per entry, so a removal does not rewrite the
/// entries that follow it. Balance comes from randomized priorities, so lookup, insertion, and removal are expected
/// logarithmic in the number of entries, with a linear worst case; no stronger bound is guaranteed. The ordering is
/// positional, not a comparison of keys, so equal values held under different keys remain distinct entries.
/// <para>
/// A key-to-node dictionary is retained alongside the tree so that a key can be located without a comparer. The tree
/// replaces the position list of the previous implementation; it does not remove the need for that dictionary.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the key.</typeparam>
internal sealed class KeyPositionIndex<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, Node> _nodes = [];

    private Node? _root;

    // Deterministic xorshift state. Priorities only need to be unpredictable relative to insertion order.
    private uint _priorityState = 2463534242;

    public int Count => _nodes.Count;

    public bool Contains(TKey key) => _nodes.ContainsKey(key);

    /// <summary>
    /// Inserts <paramref name="key"/> so that it occupies <paramref name="index"/>, shifting later entries down.
    /// </summary>
    /// <param name="index">The position the key will occupy, from zero to <see cref="Count"/> inclusive.</param>
    /// <param name="key">The key to insert.</param>
    public void InsertAt(int index, TKey key)
    {
        var node = new Node(key, NextPriority());
        _nodes.Add(key, node);

        if (_root is null)
        {
            _root = node;
            return;
        }

        var current = _root;
        var remaining = index;

        while (true)
        {
            // Every node on the descent path gains exactly one descendant.
            ++current.Size;

            var leftSize = SizeOf(current.Left);
            if (remaining <= leftSize)
            {
                if (current.Left is null)
                {
                    current.Left = node;
                    node.Parent = current;
                    break;
                }

                current = current.Left;
            }
            else
            {
                remaining -= leftSize + 1;
                if (current.Right is null)
                {
                    current.Right = node;
                    node.Parent = current;
                    break;
                }

                current = current.Right;
            }
        }

        while (node.Parent is { } parent && node.Priority > parent.Priority)
            RotateAboveParent(node);
    }

    /// <summary>
    /// Removes <paramref name="key"/> and reports the position it occupied.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>The removed position, or a negative value when the key is not tracked.</returns>
    public int Remove(TKey key)
    {
        if (!_nodes.TryGetValue(key, out var node))
            return -1;

        var index = IndexOf(node);
        _nodes.Remove(key);

        // Rotate the node down to a leaf so it can be detached without re-linking its children.
        while (node.Left is not null || node.Right is not null)
        {
            var child = node.Left is null ? node.Right! : node.Right is null ? node.Left! : node.Left.Priority > node.Right.Priority ? node.Left : node.Right;
            RotateAboveParent(child);
        }

        if (node.Parent is not { } parent)
        {
            _root = null;
        }
        else
        {
            if (ReferenceEquals(parent.Left, node))
                parent.Left = null;
            else
                parent.Right = null;

            for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
                --ancestor.Size;
        }

        node.Parent = null;
        return index;
    }

    /// <summary>
    /// Gets the current position of <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <param name="index">The position of the key, or a negative value when the key is not tracked.</param>
    /// <returns><see langword="true"/> when the key is tracked.</returns>
    public bool TryGetIndex(TKey key, out int index)
    {
        if (_nodes.TryGetValue(key, out var node))
        {
            index = IndexOf(node);
            return true;
        }

        index = -1;
        return false;
    }

    private static int IndexOf(Node node)
    {
        var index = SizeOf(node.Left);

        for (var current = node; current.Parent is { } parent; current = parent)
        {
            if (ReferenceEquals(parent.Right, current))
                index += SizeOf(parent.Left) + 1;
        }

        return index;
    }

    private static int SizeOf(Node? node) => node?.Size ?? 0;

    private static void ResetSize(Node node) => node.Size = 1 + SizeOf(node.Left) + SizeOf(node.Right);

    private uint NextPriority()
    {
        _priorityState ^= _priorityState << 13;
        _priorityState ^= _priorityState >> 17;
        _priorityState ^= _priorityState << 5;
        return _priorityState;
    }

    private void RotateAboveParent(Node node)
    {
        var parent = node.Parent!;
        var grandparent = parent.Parent;

        if (ReferenceEquals(parent.Left, node))
        {
            parent.Left = node.Right;
            if (node.Right is not null)
                node.Right.Parent = parent;

            node.Right = parent;
        }
        else
        {
            parent.Right = node.Left;
            if (node.Left is not null)
                node.Left.Parent = parent;

            node.Left = parent;
        }

        parent.Parent = node;
        node.Parent = grandparent;

        if (grandparent is null)
            _root = node;
        else if (ReferenceEquals(grandparent.Left, parent))
            grandparent.Left = node;
        else
            grandparent.Right = node;

        // Only the rotated pair change their descendant counts; every ancestor keeps the same total.
        ResetSize(parent);
        ResetSize(node);
    }

    private sealed class Node(TKey key, uint priority)
    {
        public TKey Key { get; } = key;

        public uint Priority { get; } = priority;

        public Node? Left { get; set; }

        public Node? Right { get; set; }

        public Node? Parent { get; set; }

        public int Size { get; set; } = 1;
    }
}
