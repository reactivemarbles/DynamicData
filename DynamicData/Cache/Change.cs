using System;
using System.Collections.Generic;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    ///   Container to describe a single change to a cache
    /// </summary>
    public struct Change<TObject, TKey> : IEquatable<Change<TObject, TKey>>
    {
        /// <summary>
        /// The unique key of the item which has changed
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// The  reason for the change
        /// </summary>
        public ChangeReason Reason { get; }

        /// <summary>
        /// The item which has changed
        /// </summary>
        public TObject Current { get; }

        /// <summary>
        /// The current index
        /// </summary>
        public int CurrentIndex { get; }

        /// <summary>
        /// The previous change.
        /// 
        /// This is only when Reason==ChangeReason.Replace.
        /// </summary>
        public Optional<TObject> Previous { get; }

        /// <summary>
        /// The previous change.
        /// 
        /// This is only when Reason==ChangeReason.Update or ChangeReason.Move.
        /// </summary>
        public int PreviousIndex { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
        /// </summary>
        /// <param name="reason">The reason.</param>
        /// <param name="key">The key.</param>
        /// <param name="current">The current.</param>
        /// <param name="index">The index.</param>
        public Change(ChangeReason reason, TKey key, TObject current, int index = -1)
            : this(reason, key, current, Optional.None<TObject>(), index, -1)
        {
        }

        /// <summary>
        /// Construtor for ChangeReason.Move
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="current">The current.</param>
        /// <param name="currentIndex">The CurrentIndex.</param>
        /// <param name="previousIndex">CurrentIndex of the previous.</param>
        /// <exception cref="System.ArgumentException">
        /// CurrentIndex must be greater than or equal to zero
        /// or
        /// PreviousIndex must be greater than or equal to zero
        /// </exception>
        public Change(TKey key, TObject current, int currentIndex, int previousIndex)
            : this()
        {
            if (currentIndex < 0)
                throw new ArgumentException("CurrentIndex must be greater than or equal to zero");

            if (previousIndex < 0)
                throw new ArgumentException("PreviousIndex must be greater than or equal to zero");

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
        /// <exception cref="System.ArgumentException">
        /// For ChangeReason.Add, a previous value cannot be specified
        /// or
        /// For ChangeReason.Change, must supply previous value
        /// </exception>
        public Change(ChangeReason reason, TKey key, TObject current, Optional<TObject> previous, int currentIndex = -1, int previousIndex = -1)
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

        #region Equality

        public static bool operator ==(Change<TObject, TKey> left, Change<TObject, TKey> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Change<TObject, TKey> left, Change<TObject, TKey> right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        ///  Determines whether the specified object, is equal to this instance.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(Change<TObject, TKey> other)
        {
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) 
                && Reason == other.Reason && EqualityComparer<TObject>.Default.Equals(Current, other.Current)
                && CurrentIndex == other.CurrentIndex && Previous.Equals(other.Previous) 
                && PreviousIndex == other.PreviousIndex;
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
            return obj is Change<TObject, TKey> && Equals((Change<TObject, TKey>) obj);
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
                var hashCode = EqualityComparer<TKey>.Default.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ (int) Reason;
                hashCode = (hashCode * 397) ^ EqualityComparer<TObject>.Default.GetHashCode(Current);
                hashCode = (hashCode * 397) ^ CurrentIndex;
                hashCode = (hashCode * 397) ^ Previous.GetHashCode();
                hashCode = (hashCode * 397) ^ PreviousIndex;
                return hashCode;
            }
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
            return $"{Reason}, Key: {Key}, Current: {Current}, Previous: {Previous}";
        }
    }
}
