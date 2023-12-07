// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
///   Container to describe a single change to a cache.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public readonly struct Change<TObject, TKey> : IEquatable<Change<TObject, TKey>>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="key">The key.</param>
    /// <param name="current">The current.</param>
    /// <param name="index">The index.</param>
    public Change(ChangeReason reason, TKey key, TObject current, int index = -1)
        : this(reason, key, current, Optional.None<TObject>(), index)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
    /// Constructor for ChangeReason.Move.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="current">The current.</param>
    /// <param name="currentIndex">The CurrentIndex.</param>
    /// <param name="previousIndex">CurrentIndex of the previous.</param>
    /// <exception cref="ArgumentException">
    /// CurrentIndex must be greater than or equal to zero
    /// or
    /// PreviousIndex must be greater than or equal to zero.
    /// </exception>
    public Change(TKey key, TObject current, int currentIndex, int previousIndex)
        : this()
    {
        if (currentIndex < 0)
        {
            throw new ArgumentException("CurrentIndex must be greater than or equal to zero");
        }

        if (previousIndex < 0)
        {
            throw new ArgumentException("PreviousIndex must be greater than or equal to zero");
        }

        Current = current;
        Previous = Optional.None<TObject>();
        Key = key;
        Reason = ChangeReason.Moved;
        CurrentIndex = currentIndex;
        PreviousIndex = previousIndex;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
    /// </summary>
    /// <param name="reason">The reason.</param>
    /// <param name="key">The key.</param>
    /// <param name="current">The current.</param>
    /// <param name="previous">The previous.</param>
    /// <param name="currentIndex">Value of the current.</param>
    /// <param name="previousIndex">Value of the previous.</param>
    /// <exception cref="ArgumentException">
    /// For ChangeReason.Add, a previous value cannot be specified
    /// or
    /// For ChangeReason.Change, must supply previous value.
    /// </exception>
    public Change(ChangeReason reason, TKey key, TObject current, in Optional<TObject> previous, int currentIndex = -1, int previousIndex = -1)
        : this()
    {
        Current = current;
        Previous = previous;
        Key = key;
        Reason = reason;
        CurrentIndex = currentIndex;
        PreviousIndex = previousIndex;

        if (reason == ChangeReason.Add && previous.HasValue)
        {
            throw new ArgumentException("For ChangeReason.Add, a previous value cannot be specified");
        }

        if (reason == ChangeReason.Update && !previous.HasValue)
        {
            throw new ArgumentException("For ChangeReason.Change, must supply previous value");
        }
    }

    /// <summary>
    /// Gets the unique key of the item which has changed.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the  reason for the change.
    /// </summary>
    public ChangeReason Reason { get; }

    /// <summary>
    /// Gets the item which has changed.
    /// </summary>
    public TObject Current { get; }

    /// <summary>
    /// Gets the current index.
    /// </summary>
    public int CurrentIndex { get; }

    /// <summary>
    /// <para>Gets the previous change.</para>
    /// <para>This is only when Reason==ChangeReason.Replace.</para>
    /// </summary>
    public Optional<TObject> Previous { get; }

    /// <summary>
    /// <para>Gets the previous change.</para>
    /// <para>This is only when Reason==ChangeReason.Update or ChangeReason.Move.</para>
    /// </summary>
    public int PreviousIndex { get; }

    /// <summary>
    ///  Determines whether the specified objects are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>If the two values are equal to each other.</returns>
    public static bool operator ==(in Change<TObject, TKey> left, in Change<TObject, TKey> right) => left.Equals(right);

    /// <summary>
    ///  Determines whether the specified objects are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>If the two values are not equal to each other.</returns>
    public static bool operator !=(in Change<TObject, TKey> left, in Change<TObject, TKey> right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(Change<TObject, TKey> other) => EqualityComparer<TKey>.Default.Equals(Key, other.Key) && Reason == other.Reason && EqualityComparer<TObject?>.Default.Equals(Current, other.Current) && CurrentIndex == other.CurrentIndex && Previous.Equals(other.Previous) && PreviousIndex == other.PreviousIndex;

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is Change<TObject, TKey> change && Equals(change);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = EqualityComparer<TKey>.Default.GetHashCode(Key);
            hashCode = (hashCode * 397) ^ (int)Reason;
            hashCode = (hashCode * 397) ^ (Current is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Current));
            hashCode = (hashCode * 397) ^ CurrentIndex;
            hashCode = (hashCode * 397) ^ Previous.GetHashCode();
            hashCode = (hashCode * 397) ^ PreviousIndex;
            return hashCode;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Reason}, Key: {Key}, Current: {Current}, Previous: {Previous}";
}
