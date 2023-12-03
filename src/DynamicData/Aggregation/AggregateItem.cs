// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// An object representing added and removed items in a continuous aggregation stream.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="AggregateItem{TObject}"/> struct.
/// </remarks>
/// <param name="type">The type.</param>
/// <param name="item">The item.</param>
public readonly struct AggregateItem<TObject>(AggregateType type, TObject item) : IEquatable<AggregateItem<TObject>>
{
    /// <summary>
    /// Gets the type.
    /// </summary>
    public AggregateType Type { get; } = type;

    /// <summary>
    /// Gets the item.
    /// </summary>
    public TObject Item { get; } = item;

    public static bool operator ==(in AggregateItem<TObject> left, in AggregateItem<TObject> right) => left.Equals(right);

    public static bool operator !=(in AggregateItem<TObject> left, in AggregateItem<TObject> right) => !(left == right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AggregateItem<TObject> aggItem && Equals(aggItem);

    /// <inheritdoc/>
    public bool Equals(AggregateItem<TObject> other) =>
        Type == other.Type && EqualityComparer<TObject>.Default.Equals(Item, other.Item);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = -1719135621;
        hashCode = (hashCode * -1521134295) + Type.GetHashCode();
        hashCode = (hashCode * -1521134295) + (Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item));
        return hashCode;
    }
}
