using System;
using System.Collections.Generic;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    //public static class ChangeEx
    //{
    //    public int GetStart
    //}

    /// <summary>
    ///   Container to describe a single change to a cache
    /// </summary>
    public sealed class Change<T> : IEquatable<Change<T>>
    {
        /// <summary>
        /// The reason for the change
        /// </summary>
        public ListChangeReason Reason { get; }

        /// <summary>
        /// A single item change
        /// </summary>
        public ItemChange<T> Item { get; }

        /// <summary>
        /// A multiple item change
        /// </summary>
        public RangeChange<T> Range { get; }

        /// <summary>
        /// Gets a value indicating whether the change is a single item change or a range change
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public ChangeType Type => Reason.GetChangeType();

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="Change{T}"/> class.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="current">The current.</param>
        /// <param name="index">The index.</param>
        public Change(ListChangeReason reason, T current, int index = -1)
            : this(reason, current, Optional.None<T>(), index, -1)
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
                throw new IndexOutOfRangeException("ListChangeReason must be a range type for a range change");

            //ignore this case because WhereReasonsAre removes the index 
            //if (reason== ListChangeReason.RemoveRange && index < 0)
            //        throw new UnspecifiedIndexException("ListChangeReason.RemoveRange should not have an index specified index");

            Reason = reason;
            Item = ItemChange<T>.Empty;
            Range = new RangeChange<T>(items, index);
        }

        /// <summary>
        /// Construtor for ChangeReason.Move
        /// </summary>
        /// <param name="current">The current.</param>
        /// <param name="currentIndex">The CurrentIndex.</param>
        /// <param name="previousIndex">CurrentIndex of the previous.</param>
        /// <exception cref="System.ArgumentException">
        /// CurrentIndex must be greater than or equal to zero
        /// or
        /// PreviousIndex must be greater than or equal to zero
        /// </exception>
        public Change(T current, int currentIndex, int previousIndex)
        {
            if (currentIndex < 0)
                throw new ArgumentException("CurrentIndex must be greater than or equal to zero");

            if (previousIndex < 0)
                throw new ArgumentException("PreviousIndex must be greater than or equal to zero");

            Reason = ListChangeReason.Moved;
            Item = new ItemChange<T>(Reason, current, Optional.None<T>(), currentIndex, previousIndex);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Change{TObject, TKey}" /> struct.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="current">The current.</param>
        /// <param name="previous">The previous.</param>
        /// <param name="currentIndex">Value of the current.</param>
        /// <param name="previousIndex">Value of the previous.</param>
        /// <exception cref="ArgumentException">
        /// For ChangeReason.Add, a previous value cannot be specified
        /// or
        /// For ChangeReason.Change, must supply previous value
        /// </exception>
        /// <exception cref="System.ArgumentException">For ChangeReason.Add, a previous value cannot be specified
        /// or
        /// For ChangeReason.Change, must supply previous value</exception>
        public Change(ListChangeReason reason, T current, Optional<T> previous, int currentIndex = -1, int previousIndex = -1)
        {
            if (reason == ListChangeReason.Add && previous.HasValue)
                throw new ArgumentException("For ChangeReason.Add, a previous value cannot be specified");
            if (reason == ListChangeReason.Replace && !previous.HasValue)
                throw new ArgumentException("For ChangeReason.Change, must supply previous value");

            if (reason == ListChangeReason.Refresh && currentIndex < 0)
                throw new ArgumentException("For ChangeReason.Refresh, must supply and index");


            Reason = reason;
            Item = new ItemChange<T>(Reason, current, previous, currentIndex, previousIndex);
        }

        #endregion

        #region Equality

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(Change<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Reason == other.Reason && Item.Equals(other.Item) && Equals(Range, other.Range);
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
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Change<T>)obj);
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
                var hashCode = (int)Reason;
                hashCode = (hashCode * 397) ^ Item.GetHashCode();
                hashCode = (hashCode * 397) ^ (Range?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(Change<T> left, Change<T> right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(Change<T> left, Change<T> right)
        {
            return !Equals(left, right);
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
            return Range != null ? $"{Reason}. {Range.Count} changes"
                : $"{Reason}. Current: {Item.Current}, Previous: {Item.Previous}";
        }
    }
}
