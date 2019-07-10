// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

        /// <inheritdoc />
        public bool Equals(ChangeStatistics other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Adds == other.Adds && Updates == other.Updates && Removes == other.Removes && Refreshes == other.Refreshes && Moves == other.Moves && Count == other.Count && Index == other.Index && LastUpdated.Equals(other.LastUpdated);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ChangeStatistics)obj);
        }

        /// <inheritdoc />
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

#pragma warning disable 1591

        public static bool operator ==(ChangeStatistics left, ChangeStatistics right)

        {
            return Equals(left, right);
        }

        public static bool operator !=(ChangeStatistics left, ChangeStatistics right)
        {
            return !Equals(left, right);
        }

        #endregion

        /// <inheritdoc />
        public override string ToString()
        {
            return $"CurrentIndex: {Index}, Adds: {Adds}, Updates: {Updates}, Removes: {Removes}, Refreshes: {Refreshes}, Count: {Count}, Timestamp: {LastUpdated}";
        }

    }
}
