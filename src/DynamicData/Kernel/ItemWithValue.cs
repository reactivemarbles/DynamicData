// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Container for an item and it's Value from a list
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public readonly struct ItemWithValue<TObject, TValue> : IEquatable<ItemWithValue<TObject, TValue>>
    {
        /// <summary>
        /// Gets the item.
        /// </summary>
        public TObject Item { get; }

        /// <summary>
        /// Gets the Value.
        /// </summary>
        public TValue Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="value">The Value.</param>
        public ItemWithValue(TObject item, TValue value)
        {
            Item = item;
            Value = value;
        }

        #region Equality 

        /// <inheritdoc />
        public bool Equals(ItemWithValue<TObject, TValue> other)
        {
            return EqualityComparer<TObject>.Default.Equals(Item, other.Item) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is ItemWithValue<TObject, TValue> && Equals((ItemWithValue<TObject, TValue>) obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<TObject>.Default.GetHashCode(Item) * 397) ^ EqualityComparer<TValue>.Default.GetHashCode(Value);
            }
        }

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

        #endregion

        /// <inheritdoc />
        public override string ToString() => $"{Item} ({Value})";
    }
}
