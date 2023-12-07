// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// Container holding sender and latest property value.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
public sealed class PropertyValue<TObject, TValue> : IEquatable<PropertyValue<TObject, TValue>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyValue{TObject, TValue}"/> class.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="value">The value.</param>
    public PropertyValue(TObject sender, TValue value)
    {
        Sender = sender;
        Value = value;
    }

    internal PropertyValue(TObject sender)
    {
        Sender = sender;
        UnobtainableValue = true;
        Value = default;
    }

    /// <summary>
    /// Gets the Sender.
    /// </summary>
    public TObject Sender { get; }

    /// <summary>
    /// Gets latest observed value.
    /// </summary>
    public TValue? Value { get; }

    /// <summary>
    /// Gets a value indicating whether flag to indicated that the value was unobtainable when observing a deeply nested struct.
    /// </summary>
    internal bool UnobtainableValue { get; }

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(PropertyValue<TObject, TValue>? left, PropertyValue<TObject, TValue>? right) => Equals(left, right);

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(PropertyValue<TObject, TValue>? left, PropertyValue<TObject, TValue>? right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(PropertyValue<TObject, TValue>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other.Value is null && Value is null)
        {
            return true;
        }

        if (other.Value is null || Value is null)
        {
            return false;
        }

        return EqualityComparer<TObject>.Default.Equals(Sender, other.Sender) && EqualityComparer<TValue>.Default.Equals(Value, other.Value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PropertyValue<TObject, TValue> propertyValue && Equals(propertyValue);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Sender is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Sender) * 397) ^ (Value is null ? 0 : EqualityComparer<TValue?>.Default.GetHashCode(Value));
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Sender} ({Value})";
}
