// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Kernel;
#else

namespace DynamicData.Kernel;
#endif

/// <summary>
/// Container for an item and it's index from a list.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> struct.
/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
/// </remarks>
/// <param name="Item">The item.</param>
/// <param name="Index">The index.</param>
public readonly record struct ItemWithIndex<T>(T Item, int Index)
{
    /// <summary>Returns the hash code for this instance.</summary>
    /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
    public override int GetHashCode() => Item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Item);

    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"{Item} ({Index})";
}
