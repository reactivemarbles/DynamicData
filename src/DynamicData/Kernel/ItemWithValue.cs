// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel;

/// <summary>
/// Container for an item and it's Value from a list.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ItemWithValue{TObject, TValue}"/> struct.
/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
/// </remarks>
/// <param name="item">The item.</param>
/// <param name="value">The Value.</param>
public readonly struct ItemWithValue<TObject, TValue>(TObject item, TValue value) : IEquatable<ItemWithValue<TObject, TValue>>
{
    /// <summary>
    /// Gets the item.
    /// </summary>
    public TObject Item { get; } = item;

    /// <summary>
    /// Gets the Value.
    /// </summary>
    public TValue Value { get; } = value;

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(in ItemWithValue<TObject, TValue> left, in ItemWithValue<TObject, TValue> right) => Equals(left, right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(in ItemWithValue<TObject, TValue> left, in ItemWithValue<TObject, TValue> right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(ItemWithValue<TObject, TValue> other) => EqualityComparer<TObject>.Default.Equals(Item, other.Item) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null)
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
