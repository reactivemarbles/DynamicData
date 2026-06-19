// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// Container holding sender and latest property value.
/// </summary>
/// <param name="Sender"> Gets the Sender. </param>
/// <param name="Value"> Gets latest observed value. </param>
/// <param name="UnobtainableValue"> Gets a value indicating whether flag to indicated that the value was unobtainable when observing a deeply nested struct. </param>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
public sealed record PropertyValue<TObject, TValue>(TObject Sender, TValue? Value, bool UnobtainableValue)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyValue{TObject, TValue}"/> class.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="value">The value.</param>
    public PropertyValue(TObject sender, TValue value)
        : this(sender, value, default)
    {
    }

    internal PropertyValue(TObject sender)
        : this(sender, default, true)
    {
    }

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
