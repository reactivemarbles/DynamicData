// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Container for an item and it's Value from a list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public readonly struct ItemWithValue<TObject, TValue> : IEquatable<ItemWithValue<TObject, TValue>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemWithValue{TObject, TValue}"/> struct.
        /// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="value">The Value.</param>
        public ItemWithValue(TObject item, TValue value)
        {
            Item = item;
            Value = value;
        }

        /// <summary>
        /// Gets the item.
        /// </summary>
        public TObject Item { get; }

        /// <summary>
        /// Gets the Value.
        /// </summary>
        public TValue Value { get; }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(ItemWithValue<TObject, TValue> left, ItemWithValue<TObject, TValue> right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(ItemWithValue<TObject, TValue> left, ItemWithValue<TObject, TValue> right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public bool Equals(ItemWithValue<TObject, TValue> other)
        {
            return EqualityComparer<TObject>.Default.Equals(Item, other.Item) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is ItemWithValue<TObject, TValue> itemWithValue && Equals(itemWithValue);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item) * 397) ^ (Value is null ? 0 : EqualityComparer<TValue>.Default.GetHashCode(Value));
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"{Item} ({Value})";
    }
}