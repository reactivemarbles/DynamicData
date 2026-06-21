// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the ExpirableItem class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <param name="value">The value value.</param>
/// <param name="dateTime">The dateTime value.</param>
/// <param name="index">The index value.</param>
internal sealed class ExpirableItem<TObject>(TObject value, DateTime dateTime, long index) : IEquatable<ExpirableItem<TObject>>
{
    /// <summary>
    /// Gets the ExpireAt value.
    /// </summary>
    public DateTime ExpireAt { get; } = dateTime;

    /// <summary>
    /// Gets the Index value.
    /// </summary>
    public long Index { get; } = index;

    /// <summary>
    /// Gets the Item value.
    /// </summary>
    public TObject Item { get; } = value;

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator ==(ExpirableItem<TObject> left, ExpirableItem<TObject> right) => Equals(left, right);

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator !=(ExpirableItem<TObject> left, ExpirableItem<TObject> right) => !Equals(left, right);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(ExpirableItem<TObject>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EqualityComparer<TObject>.Default.Equals(Item, other.Item) && ExpireAt.Equals(other.ExpireAt) && Index == other.Index;
    }

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ExpirableItem<TObject> item && Equals(item);
    }

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item);
            hashCode = (hashCode * 397) ^ ExpireAt.GetHashCode();
            hashCode = (hashCode * 397) ^ Index.GetHashCode();
            return hashCode;
        }
    }

    /// <summary>
    /// Executes the ToString operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"{Item} @ {ExpireAt}";
}
