using System;

namespace DynamicData.Diagnostics
{
    /// <summary>
    ///     Object used to capture accumulated changes
    /// </summary>
    public class ChangeStatistics : IEquatable<ChangeStatistics>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public ChangeStatistics()
        {
            Index = -1;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public ChangeStatistics(int index, int adds, int updates, int removes, int refreshes, int moves, int count)
        {
            Index = index;
            Adds = adds;
            Updates = updates;
            Removes = removes;
            Refreshes = refreshes;
            Moves = moves;
            Count = count;
        }

        /// <summary>
        ///     Gets the adds.
        /// </summary>
        /// <value>
        ///     The adds.
        /// </value>
        public int Adds { get; }

        /// <summary>
        ///     Gets the updates.
        /// </summary>
        /// <value>
        ///     The updates.
        /// </value>
        public int Updates { get; }

        /// <summary>
        ///     Gets the removes.
        /// </summary>
        /// <value>
        ///     The removes.
        /// </value>
        public int Removes { get; }

        /// <summary>
        ///     Gets the refreshes.
        /// </summary>
        /// <value>
        ///     The refreshes.
        /// </value>
        public int Refreshes { get; }

        /// <summary>
        ///     Gets the count.
        /// </summary>
        /// <value>
        ///     The count.
        /// </value>
        public int Count { get; }

        /// <summary>
        ///     Gets the index.
        /// </summary>
        /// <value>
        ///     The index.
        /// </value>
        public int Index { get; }

        /// <summary>
        ///     Gets the moves.
        /// </summary>
        /// <value>
        ///     The moves.
        /// </value>
        public int Moves { get; }

        /// <summary>
        ///     Gets the last updated.
        /// </summary>
        /// <value>
        ///     The last updated.
        /// </value>
        public DateTime LastUpdated { get; } = DateTime.Now;

        #region Equality members

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(ChangeStatistics other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Adds == other.Adds && Updates == other.Updates && Removes == other.Removes && Refreshes == other.Refreshes && Moves == other.Moves && Count == other.Count && Index == other.Index && LastUpdated.Equals(other.LastUpdated);
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
            return Equals((ChangeStatistics)obj);
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
                var hashCode = Adds;
                hashCode = (hashCode * 397) ^ Updates;
                hashCode = (hashCode * 397) ^ Removes;
                hashCode = (hashCode * 397) ^ Refreshes;
                hashCode = (hashCode * 397) ^ Moves;
                hashCode = (hashCode * 397) ^ Count;
                hashCode = (hashCode * 397) ^ Index;
                hashCode = (hashCode * 397) ^ LastUpdated.GetHashCode();
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
        public static bool operator ==(ChangeStatistics left, ChangeStatistics right)
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
        public static bool operator !=(ChangeStatistics left, ChangeStatistics right)
        {
            return !Equals(left, right);
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
                    "CurrentIndex: {0}, Adds: {1}, Updates: {2}, Removes: {3}, Refreshes: {4}, Count: {5}, Timestamp: {6}",
                    Index, Adds, Updates, Removes, Refreshes, Count, LastUpdated);
        }

        #endregion
    }
}
