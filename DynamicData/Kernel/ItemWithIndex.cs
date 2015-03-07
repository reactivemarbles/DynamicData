using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
	/// <summary>
	/// Container for an item and it's index from a list
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ItemWithIndex<T> : IEquatable<ItemWithIndex<T>>
	{
		/// <summary>
		/// Gets the item.
		/// </summary>
		public T Item  { get; }

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

		public bool Equals(ItemWithIndex<T> other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return EqualityComparer<T>.Default.Equals(Item, other.Item) && Index == other.Index;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ItemWithIndex<T>) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (EqualityComparer<T>.Default.GetHashCode(Item)*397) ^ Index;
			}
		}

		public static bool operator ==(ItemWithIndex<T> left, ItemWithIndex<T> right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(ItemWithIndex<T> left, ItemWithIndex<T> right)
		{
			return !Equals(left, right);
		}

		#endregion

		public override string ToString()
		{
			return string.Format("{0} ({1})", Item, Index);
		}
	}
}