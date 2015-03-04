

using System;


namespace DynamicData.Diagnostics
{
	/// <summary>
	///     Object used to capture accumulated changes
	/// </summary>
	public class ChangeStatistics
	{
		private readonly int _adds;
		private readonly int _count;
		private readonly int _evaluates;
		private readonly int _index;
		private readonly int _removes;
		private readonly int _moves;
		private readonly DateTime _timestamp = DateTime.Now;
		private readonly int _updates;


		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Object" /> class.
		/// </summary>
		public ChangeStatistics()
		{
			_index = -1;
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:System.Object" /> class.
		/// </summary>
		public ChangeStatistics(int index, int adds, int updates, int removes, int evaluates, int moves, int count)
		{
			_index = index;
			_adds = adds;
			_updates = updates;
			_removes = removes;
			_evaluates = evaluates;
			_moves = moves;
			_count = count;
		}

		/// <summary>
		///     Gets the adds.
		/// </summary>
		/// <value>
		///     The adds.
		/// </value>
		public int Adds
		{
			get { return _adds; }
		}

		/// <summary>
		///     Gets the updates.
		/// </summary>
		/// <value>
		///     The updates.
		/// </value>
		public int Updates
		{
			get { return _updates; }
		}

		/// <summary>
		///     Gets the removes.
		/// </summary>
		/// <value>
		///     The removes.
		/// </value>
		public int Removes
		{
			get { return _removes; }
		}

		/// <summary>
		///     Gets the evaluates.
		/// </summary>
		/// <value>
		///     The evaluates.
		/// </value>
		public int Evaluates
		{
			get { return _evaluates; }
		}

		/// <summary>
		///     Gets the count.
		/// </summary>
		/// <value>
		///     The count.
		/// </value>
		public int Count
		{
			get { return _count; }
		}

		/// <summary>
		///     Gets the index.
		/// </summary>
		/// <value>
		///     The index.
		/// </value>
		public int Index
		{
			get { return _index; }
		}

		/// <summary>
		///     Gets the moves.
		/// </summary>
		/// <value>
		///     The moves.
		/// </value>
		public int Moves
		{
			get { return _moves; }
		}

		/// <summary>
		///     Gets the last updated.
		/// </summary>
		/// <value>
		///     The last updated.
		/// </value>
		public DateTime LastUpdated
		{
			get { return _timestamp; }
		}

		#region Equality members

		protected bool Equals(ChangeStatistics other)
		{
			return _index == other._index
			       && _adds == other._adds
			       && _updates == other._updates && _removes == other._removes && _evaluates == other._evaluates &&
			       _count == other._count && _timestamp.Equals(other._timestamp);
		}

		/// <summary>
		///     Determines whether the specified <see cref="T:System.Object" /> is equal to the current
		///     <see cref="T:System.Object" />.
		/// </summary>
		/// <returns>
		///     true if the specified <see cref="T:System.Object" /> is equal to the current <see cref="T:System.Object" />;
		///     otherwise, false.
		/// </returns>
		/// <param name="obj">
		///     The <see cref="T:System.Object" /> to compare with the current <see cref="T:System.Object" />.
		/// </param>
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((ChangeStatistics) obj);
		}

		/// <summary>
		///     Serves as a hash function for a particular type.
		/// </summary>
		/// <returns>
		///     A hash code for the current <see cref="T:System.Object" />.
		/// </returns>
		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = _index;
				hashCode = (hashCode*397) ^ _adds;
				hashCode = (hashCode*397) ^ _updates;
				hashCode = (hashCode*397) ^ _removes;
				hashCode = (hashCode*397) ^ _evaluates;
				hashCode = (hashCode*397) ^ _count;
				hashCode = (hashCode*397) ^ _timestamp.GetHashCode();
				return hashCode;
			}
		}

		#endregion

		#region Formatting Members

		/// <summary>
		///     Returns a <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
		/// </summary>
		/// <returns>
		///     A <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
		/// </returns>
		public override string ToString()
		{
			return
				string.Format(
					"CurrentIndex: {0}, Adds: {1}, Updates: {2}, Removes: {3}, Evaluates: {4}, Count: {5}, Timestamp: {6}",
					_index, _adds, _updates, _removes, _evaluates, _count, _timestamp);
		}

		#endregion
	}
}