// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Container to describe a single change to a cache.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public readonly struct ItemChange<T> : IEquatable<ItemChange<T>>
    where T : notnull
{
    /// <summary>
    /// An empty change.
    /// </summary>
    public static readonly ItemChange<T> Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemChange{T}" /> struct.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="current">The current.</param>
    /// <param name="previous">The previous.</param>
    /// <param name="currentIndex">Value of the current.</param>
    /// <param name="previousIndex">Value of the previous.</param>
    public ItemChange(ListChangeReason reason, T current, in Optional<T> previous, int currentIndex = -1, int previousIndex = -1)
        : this()
    {
        Reason = reason;
        Current = current;
        Previous = previous;
        CurrentIndex = currentIndex;
        PreviousIndex = previousIndex;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemChange{T}"/> struct.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="current">The current.</param>
    /// <param name="currentIndex">Index of the current.</param>
    public ItemChange(ListChangeReason reason, T current, int currentIndex)
        : this()
    {
        Reason = reason;
        Current = current;
        CurrentIndex = currentIndex;
        PreviousIndex = -1;
        Previous = Optional<T>.None;
    }

    /// <summary>
    /// Gets the reason for the change.
    /// </summary>
    public ListChangeReason Reason { get; }

    /// <summary>
    /// Gets the item which has changed.
    /// </summary>
    public T Current { get; }

    /// <summary>
    /// Gets the current index.
    /// </summary>
    public int CurrentIndex { get; }

    /// <summary>
    /// <para>Gets the previous change.</para>
    /// <para>This is only when Reason==ChangeReason.Replace.</para>
    /// </summary>
    public Optional<T> Previous { get; }

    /// <summary>
    /// <para>Gets the previous index.</para>
    /// <para>This is only when Reason==ChangeReason.Replace or ChangeReason.Move.</para>
    /// </summary>
    public int PreviousIndex { get; }

    /// <summary>
    ///  Determines whether the specified objects are equal.
    /// </summary>
    /// <param name="left">The left hand side to compare.</param>
    /// <param name="right">The right hand side to compare.</param>
    /// <returns>If the two values are equal.</returns>
    public static bool operator ==(in ItemChange<T> left, in ItemChange<T> right) => left.Equals(right);

    /// <summary>
    ///  Determines whether the specified objects are not equal.
    /// </summary>
    /// <param name="left">The left hand side to compare.</param>
    /// <param name="right">The right hand side to compare.</param>
    /// <returns>If the two values are not equal.</returns>
    public static bool operator !=(in ItemChange<T> left, in ItemChange<T> right) => !left.Equals(right);

    /// <summary>
    ///  Determines whether the specified object, is equal to this instance.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>If the value is equal.</returns>
    public bool Equals(ItemChange<T> other) => EqualityComparer<T>.Default.Equals(Current, other.Current) && CurrentIndex == other.CurrentIndex && Previous.Equals(other.Previous) && PreviousIndex == other.PreviousIndex;

    /// <summary>
    /// Determines whether the specified <see cref="object" />, is equal to this instance.
    /// </summary>
    /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
    /// <returns>
    ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is ItemChange<T> change && Equals(change);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
    /// </returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Current is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Current);
            hashCode = (hashCode * 397) ^ CurrentIndex;
            hashCode = (hashCode * 397) ^ Previous.GetHashCode();
            hashCode = (hashCode * 397) ^ PreviousIndex;
            return hashCode;
        }
    }

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString() => $"Current: {Current}, Previous: {Previous}";
}
