// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
///   Container to describe a single change to a cache.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public sealed class Change<T> : IEquatable<Change<T>>
    where T : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> class.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="current">The current.</param>
    /// <param name="index">The index.</param>
    public Change(ListChangeReason reason, T current, int index = -1)
        : this(reason, current, ReactiveUI.Primitives.Optional<T>.None, index)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> class.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="items">The items.</param>
    /// <param name="index">The index.</param>
    public Change(ListChangeReason reason, IEnumerable<T> items, int index = -1)
    {
        if (reason.GetChangeType() == ChangeType.Item)
        {
            throw new IndexOutOfRangeException("ListChangeReason must be a range type for a range change");
        }

        //// ignore this case because WhereReasonsAre removes the index
        //// if (reason== ListChangeReason.RemoveRange && index < 0)
        ////        throw new UnspecifiedIndexException("ListChangeReason.RemoveRange should not have an index specified index");

        Reason = reason;
        Item = ItemChange<T>.Empty;
        Range = new RangeChange<T>(items, index);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> class.
    /// Constructor for <see cref="ListChangeReason.Moved"/>.
    /// </summary>
    /// <param name="current">The current.</param>
    /// <param name="currentIndex">The CurrentIndex.</param>
    /// <param name="previousIndex">CurrentIndex of the previous.</param>
    /// <exception cref="ArgumentException">
    /// CurrentIndex must be greater than or equal to zero
    /// or
    /// PreviousIndex must be greater than or equal to zero.
    /// </exception>
    public Change(T current, int currentIndex, int previousIndex)
    {
        if (currentIndex < 0)
        {
            throw new ArgumentException("CurrentIndex must be greater than or equal to zero");
        }

        if (previousIndex < 0)
        {
            throw new ArgumentException("PreviousIndex must be greater than or equal to zero");
        }

        Reason = ListChangeReason.Moved;
        Item = new ItemChange<T>(Reason, current, ReactiveUI.Primitives.Optional<T>.None, currentIndex, previousIndex);
        Range = RangeChange<T>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{T}"/> class.
    /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="current">The current.</param>
    /// <param name="previous">The previous.</param>
    /// <param name="currentIndex">Value of the current.</param>
    /// <param name="previousIndex">Value of the previous.</param>
    /// <exception cref="ArgumentException">
    /// For <see cref="ListChangeReason.Add"/>, a previous value cannot be specified
    /// or
    /// For <see cref="ListChangeReason.Replace"/>, must supply previous value.
    /// or
    /// For <see cref="ListChangeReason.Refresh"/>, must supply an index.
    /// </exception>
    public Change(ListChangeReason reason, T current, in ReactiveUI.Primitives.Optional<T> previous, int currentIndex = -1, int previousIndex = -1)
    {
        if (reason == ListChangeReason.Add && previous.HasValue)
        {
            throw new ArgumentException("For ListChangeReason.Add, a previous value cannot be specified");
        }

        if (reason == ListChangeReason.Replace && !previous.HasValue)
        {
            throw new ArgumentException("For ListChangeReason.Replace, must supply previous value");
        }

        if (reason == ListChangeReason.Refresh && currentIndex < 0)
        {
            throw new ArgumentException("For ListChangeReason.Refresh, must supply an index");
        }

        Reason = reason;
        Item = new ItemChange<T>(Reason, current, previous, currentIndex, previousIndex);
        Range = RangeChange<T>.Empty;
    }

    /// <summary>
    /// Gets a single item change.
    /// </summary>
    public ItemChange<T> Item { get; }

    /// <summary>
    /// Gets a multiple item change.
    /// </summary>
    public RangeChange<T> Range { get; }

    /// <summary>
    /// Gets the reason for the change.
    /// </summary>
    public ListChangeReason Reason { get; }

    /// <summary>
    /// Gets a value indicating whether the change is a single item change or a range change.
    /// </summary>
    /// <value>
    /// The type.
    /// </value>
    public ChangeType Type => Reason.GetChangeType();

    /// <summary>
    /// Determines whether two <see cref="Change{T}"/> instances are equal.
    /// </summary>
    /// <param name="left">The left change to compare.</param>
    /// <param name="right">The right change to compare.</param>
    /// <returns><see langword="true"/> when the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Change<T> left, Change<T> right) => Equals(left, right);

    /// <summary>
    /// Determines whether two <see cref="Change{T}"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left change to compare.</param>
    /// <param name="right">The right change to compare.</param>
    /// <returns><see langword="true"/> when the values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Change<T> left, Change<T> right) => !Equals(left, right);

    /// <inheritdoc />
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(Change<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Reason == other.Reason && Item.Equals(other.Item) && Equals(Range, other.Range);
    }

    /// <inheritdoc />
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

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((Change<T>)obj);
    }

    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)Reason;
            hashCode = (hashCode * 397) ^ Item.GetHashCode();
            hashCode = (hashCode * 397) ^ Range.GetHashCode();
            return hashCode;
        }
    }

    /// <inheritdoc />
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"{Reason}. {Range.Count} changes";
}
