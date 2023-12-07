// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel;

/// <summary>
/// Container for an item and it's index from a list.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> struct.
/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
/// </remarks>
/// <param name="item">The item.</param>
/// <param name="index">The index.</param>
public readonly struct ItemWithIndex<T>(T item, int index) : IEquatable<ItemWithIndex<T>>
{
    /// <summary>
    /// Gets the item.
    /// </summary>
    public T Item { get; } = item;

    /// <summary>
    /// Gets the index.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>Returns a value that indicates whether the values of two <see cref="ItemWithIndex{T}" /> objects are equal.</summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
    public static bool operator ==(in ItemWithIndex<T> left, in ItemWithIndex<T> right) => left.Equals(right);

    /// <summary>Returns a value that indicates whether two <see cref="ItemWithIndex{T}" /> objects have different values.</summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
    public static bool operator !=(in ItemWithIndex<T> left, in ItemWithIndex<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(ItemWithIndex<T> other) => EqualityComparer<T>.Default.Equals(Item, other.Item);

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is ItemWithIndex<T> itemWithIndex && Equals(itemWithIndex);
    }

    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
    public override int GetHashCode() => Item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Item);

    /// <inheritdoc />
    public override string ToString() => $"{Item} ({Index})";
}
