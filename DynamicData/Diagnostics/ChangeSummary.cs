
namespace DynamicData.Diagnostics
{
    /// <summary>
    /// Accumulates change statics
    /// </summary>
    public class ChangeSummary
    {
        private readonly int _index;

        /// <summary>
        /// An empty instance of change summary
        /// </summary>
        public static readonly ChangeSummary Empty = new ChangeSummary();

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public ChangeSummary(int index, ChangeStatistics latest, ChangeStatistics overall)
        {
            Latest = latest;
            Overall = overall;
            _index = index;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        private ChangeSummary()
        {
            _index = -1;
            Latest = new ChangeStatistics();
            Overall = new ChangeStatistics();
        }

        /// <summary>
        /// Gets the latest change
        /// </summary>
        /// <value>
        /// The latest.
        /// </value>
        public ChangeStatistics Latest { get; }

        /// <summary>
        /// Gets the overall change count
        /// </summary>
        /// <value>
        /// The overall.
        /// </value>
        public ChangeStatistics Overall { get; }

        #region Equality members

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(ChangeSummary other)
        {
            return _index == other._index && Equals(Latest, other.Latest) && Equals(Overall, other.Overall);
        }

        /// <summary>
        ///     Determines whether the specified <see cref="T:System.Object" /> is equal to the current <see cref="T:System.Object" />.
        /// </summary>
        /// <returns>
        ///     true if the specified <see cref="T:System.Object" /> is equal to the current <see cref="T:System.Object" />; otherwise, false.
        /// </returns>
        /// <param name="obj">
        ///     The <see cref="T:System.Object" /> to compare with the current <see cref="T:System.Object" />.
        /// </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ChangeSummary)obj);
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
                hashCode = (hashCode * 397) ^ (Latest?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Overall?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #endregion

        /// <summary>
        ///     Returns a <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
        /// </summary>
        /// <returns>
        ///     A <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
        /// </returns>
        public override string ToString()
        {
            return $"CurrentIndex: {_index}, Latest Count: {Latest.Count}, Overall Count: {Overall.Count}";
        }
    }
}
