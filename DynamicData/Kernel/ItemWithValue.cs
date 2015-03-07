using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{

	/// <summary>
	/// Container for an item and it's Value from a list
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ItemWithValue<T,V> : IEquatable<ItemWithValue<T,V>>
	{
		/// <summary>
		/// Gets the item.
		/// </summary>
		public T Item { get; }

		/// <summary>
		/// Gets the Value.
		/// </summary>
		public V Value { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="value">The Value.</param>
		public ItemWithValue(T item, V value)
		{
			Item = item;
			Value = value;
		}

		#region Equality 

		public bool Equals(ItemWithValue<T, V> other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return EqualityComparer<T>.Default.Equals(Item, other.Item);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ItemWithValue<T, V>) obj);
		}

		public override int GetHashCode()
		{
			return EqualityComparer<T>.Default.GetHashCode(Item);
		}

		public static bool operator ==(ItemWithValue<T, V> left, ItemWithValue<T, V> right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(ItemWithValue<T, V> left, ItemWithValue<T, V> right)
		{
			return !Equals(left, right);
		}

		#endregion

		public override string ToString()
		{
			return string.Format("{0} ({1})", Item, Value);
		}

	}
}