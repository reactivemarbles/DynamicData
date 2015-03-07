using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{

	/// <summary>
	/// Container for an item and it's Value from a list
	/// </summary>
	/// <typeparam name="TObject"></typeparam>
	public class ItemWithValue<TObject,TValue> : IEquatable<ItemWithValue<TObject,TValue>>
	{
		/// <summary>
		/// Gets the item.
		/// </summary>
		public TObject Item { get; }

		/// <summary>
		/// Gets the Value.
		/// </summary>
		public TValue Value { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ItemWithIndex{T}"/> class.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="value">The Value.</param>
		public ItemWithValue(TObject item, TValue value)
		{
			Item = item;
			Value = value;
		}

		#region Equality 

		public bool Equals(ItemWithValue<TObject, TValue> other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return EqualityComparer<TObject>.Default.Equals(Item, other.Item);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ItemWithValue<TObject, TValue>) obj);
		}

		public override int GetHashCode()
		{
			return EqualityComparer<TObject>.Default.GetHashCode(Item);
		}

		public static bool operator ==(ItemWithValue<TObject, TValue> left, ItemWithValue<TObject, TValue> right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(ItemWithValue<TObject, TValue> left, ItemWithValue<TObject, TValue> right)
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