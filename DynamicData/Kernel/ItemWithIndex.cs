using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Container for an item and it's index from a list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ItemWithIndex<T> : IEquatable<ItemWithIndex<T>>
    {
        /// <summary>
        /// Gets the item.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// Gets the index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="index">The index.</param>
        public ItemWithIndex(T item, int index)
        {
            Item = item;
            Index = index;
        }

        #region Equality 

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(ItemWithIndex<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<T>.Default.Equals(Item, other.Item) && Index == other.Index;
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
            return Equals((ItemWithIndex<T>)obj);
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
                return (EqualityComparer<T>.Default.GetHashCode(Item) * 397) ^ Index;
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
        public static bool operator ==(ItemWithIndex<T> left, ItemWithIndex<T> right)
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
        public static bool operator !=(ItemWithIndex<T> left, ItemWithIndex<T> right)
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
            return string.Format("{0} ({1})", Item, Index);
        }
    }
}
