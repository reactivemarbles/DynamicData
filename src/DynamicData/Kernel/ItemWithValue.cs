// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Kernel;
#else

namespace DynamicData.Kernel;
#endif

/// <summary>
/// Container for an item and it's Value from a list.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ItemWithValue{TObject, TValue}"/> struct.
/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
/// </remarks>
/// <param name="Item">The item.</param>
/// <param name="Value">The Value.</param>
public readonly record struct ItemWithValue<TObject, TValue>(TObject Item, TValue Value)
{
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
