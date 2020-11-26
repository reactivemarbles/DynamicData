// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Container for an item and it's index from a list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    public readonly struct ItemWithIndex<T> : IEquatable<ItemWithIndex<T>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> struct.
        /// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="index">The index.</param>
        public ItemWithIndex(T item, int index)
        {
            Item = item;
            Index = index;
        }

        /// <summary>
        /// Gets the item.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// Gets the index.
        /// </summary>
        public int Index { get; }

        /// <summary>Returns a value that indicates whether the values of two <see cref="ItemWithIndex{T}" /> objects are equal.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
        public static bool operator ==(ItemWithIndex<T> left, ItemWithIndex<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>Returns a value that indicates whether two <see cref="ItemWithIndex{T}" /> objects have different values.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
        public static bool operator !=(ItemWithIndex<T> left, ItemWithIndex<T> right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public bool Equals(ItemWithIndex<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Item, other.Item);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is ItemWithIndex<T> itemWithIndex && Equals(itemWithIndex);
        }

        /// <summary>Returns the hash code for this instance.</summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return Item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Item);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Item} ({Index})";
        }
    }
}