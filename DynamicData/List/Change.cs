using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	///   Container to describe a single change to a cache
	/// </summary>
	public struct Change<T> : IEquatable<Change<T>>
	{
		#region Fields

		private readonly T _current;

		public readonly static Change<T> Empty = new Change<T>();

		#endregion

		#region Construction


		/// <summary>
		/// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
		/// </summary>
		/// <param name="reason">The reason.</param>
		/// <param name="current">The current.</param>
		/// <param name="index">The index.</param>
		public Change(ChangeReason reason, T current, int index = -1)
			: this(reason, current, Optional.None<T>(), index, -1)
		{

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
			: this()
		{
			if (currentIndex < 0)
				throw new ArgumentException("CurrentIndex must be greater than or equal to zero");

			if (previousIndex < 0)
				throw new ArgumentException("PreviousIndex must be greater than or equal to zero");

			_current = current;
			Previous = Optional.None<T>();
			Reason = ChangeReason.Moved;
			CurrentIndex = currentIndex;
			PreviousIndex = previousIndex;

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Change{TObject, TKey}"/> struct.
		/// </summary>
		/// <param name="reason">The reason.</param>
		/// <param name="current">The current.</param>
		/// <param name="previous">The previous.</param>
		/// <param name="currentIndex">Value of the current.</param>
		/// <param name="previousIndex">Value of the previous.</param>
		/// <exception cref="System.ArgumentException">
		/// For ChangeReason.Add, a previous value cannot be specified
		/// or
		/// For ChangeReason.Change, must supply previous value
		/// </exception>
		public Change(ChangeReason reason, T current, Optional<T> previous, int currentIndex = -1, int previousIndex = -1)
			: this()
		{
			_current = current;
			Previous = previous;
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

		#endregion

		#region Properties


		/// <summary>
		/// The  reason for the change
		/// </summary>
		public ChangeReason Reason { get; }


		/// <summary>
		/// The item which has changed
		/// </summary>
		public T Current => _current;

		/// <summary>
		/// The current index
		/// </summary>
		public int CurrentIndex { get; }

		/// <summary>
		/// The previous change.
		/// 
		/// This is only when Reason==ChangeReason.Update.
		/// </summary>
		public Optional<T> Previous { get; }

		/// <summary>
		/// The previous change.
		/// 
		/// This is only when Reason==ChangeReason.Update or ChangeReason.Move.
		/// </summary>
		public int PreviousIndex { get; }

		#endregion

		#region Overrides

		public override string ToString()
		{
			return string.Format("{0}, Current: {1}, Previous: {2}", Reason, Current, Previous);
		}

		#endregion

		#region IEquatable<Change<T>> Members

		public bool Equals(Change<T> other)
		{
			return Reason == other.Reason && EqualityComparer<T>.Default.Equals(_current, other._current) && Previous.Equals(other.Previous);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is Change<T> && Equals((Change<T>)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (int)Reason;
				hashCode = (hashCode * 397) ^ EqualityComparer<T>.Default.GetHashCode(_current);
				hashCode = (hashCode * 397) ^ Previous.GetHashCode();
				return hashCode;
			}
		}

		public static bool operator ==(Change<T> left, Change<T> right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Change<T> left, Change<T> right)
		{
			return !left.Equals(right);
		}

		#endregion
	}
}