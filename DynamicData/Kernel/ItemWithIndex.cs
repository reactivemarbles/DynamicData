using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Container for an item and it's index from a list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ItemWithIndex<T> : IEquatable<ItemWithIndex<T>>
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

        /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ItemWithIndex<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Item, other.Item);
        }

        /// <summary>Indicates whether this instance and a specified object are equal.</summary>
        /// <returns>true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false. </returns>
        /// <param name="obj">The object to compare with the current instance. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ItemWithIndex<T> && Equals((ItemWithIndex<T>) obj);
        }

        /// <summary>Returns the hash code for this instance.</summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(Item);
        }

        /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.Kernel.ItemWithIndex`1" /> objects are equal.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
        public static bool operator ==(ItemWithIndex<T> left, ItemWithIndex<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.Kernel.ItemWithIndex`1" /> objects have different values.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
        public static bool operator !=(ItemWithIndex<T> left, ItemWithIndex<T> right)
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
            return $"{Item} ({Index})";
        }
    }
}
