// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Aggregation;
#else

namespace DynamicData.Aggregation;
#endif

/// <summary>
/// An object representing added and removed items in a continuous aggregation stream.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="AggregateItem{TObject}"/> struct.
/// </remarks>
/// <param name="Type">The type.</param>
/// <param name="Item">The item.</param>
public readonly record struct AggregateItem<TObject>(AggregateType Type, TObject Item)
{
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = -1719135621;
        hashCode = (hashCode * -1521134295) + Type.GetHashCode();
        hashCode = (hashCode * -1521134295) + (Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item));
        return hashCode;
    }
}
