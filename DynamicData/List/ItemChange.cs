using System;
using System.Collections.Generic;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Container to describe a single change to a cache
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ItemChange<T> : IEquatable<ItemChange<T>>
    {
        /// <summary>
        /// An empty change
        /// </summary>
        public static readonly ItemChange<T> Empty = new ItemChange<T>();

        /// <summary>
        /// The reason for the change
        /// </summary>
        public ListChangeReason Reason { get; }

        /// <summary>
        /// The item which has changed
        /// </summary>
        public T Current { get; }

        /// <summary>
        /// The current index
        /// </summary>
        public int CurrentIndex { get; }

        /// <summary>
        /// The previous change.
        /// 
        /// This is only when Reason==ChangeReason.Replace.
        /// </summary>
        public Optional<T> Previous { get; }

        /// <summary>
        /// The previous index.
        /// 
        /// This is only when Reason==ChangeReason.Replace or ChangeReason.Move.
        /// </summary>
        public int PreviousIndex { get; }

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="Change{TObject, TKey}" /> struct.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="current">The current.</param>
        /// <param name="previous">The previous.</param>
        /// <param name="currentIndex">Value of the current.</param>
        /// <param name="previousIndex">Value of the previous.</param>
        /// <exception cref="ArgumentException">For ChangeReason.Add, a previous value cannot be specified
        /// or
        /// For ChangeReason.Change, must supply previous value</exception>
        /// <exception cref="System.ArgumentException">For ChangeReason.Add, a previous value cannot be specified
        /// or
        /// For ChangeReason.Change, must supply previous value</exception>
        public ItemChange(ListChangeReason reason, T current, Optional<T> previous, int currentIndex = -1, int previousIndex = -1)
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

        #endregion

        #region Equality

        /// <summary>
        ///  Determines whether the specified object, is equal to this instance.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(ItemChange<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Current, other.Current) && CurrentIndex == other.CurrentIndex && Previous.Equals(other.Previous) && PreviousIndex == other.PreviousIndex;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ItemChange<T> && Equals((ItemChange<T>)obj);
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
                var hashCode = EqualityComparer<T>.Default.GetHashCode(Current);
                hashCode = (hashCode * 397) ^ CurrentIndex;
                hashCode = (hashCode * 397) ^ Previous.GetHashCode();
                hashCode = (hashCode * 397) ^ PreviousIndex;
                return hashCode;
            }
        }

        public static bool operator ==(ItemChange<T> left, ItemChange<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemChange<T> left, ItemChange<T> right)
        {
            return !left.Equals(right);
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"Current: {Current}, Previous: {Previous}";
        }
    }
}
