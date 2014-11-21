namespace DynamicData.Diagnostics
{
    /// <summary>
    /// Accumulates change statics
    /// </summary>
    public class ChangeSummary
    {
        private readonly int _index;
        private readonly ChangeStatistics _latest;
        private readonly ChangeStatistics _overall;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public ChangeSummary(int index, ChangeStatistics latest, ChangeStatistics overall)
        {
            _latest = latest;
            _overall = overall;
            _index = index;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public ChangeSummary()
        {
            _index = -1;
            _latest = new ChangeStatistics();
            _overall = new ChangeStatistics();
        }

        /// <summary>
        /// Gets the latest change
        /// </summary>
        /// <value>
        /// The latest.
        /// </value>
        public ChangeStatistics Latest
        {
            get { return _latest; }
        }

        /// <summary>
        /// Gets the overall change count
        /// </summary>
        /// <value>
        /// The overall.
        /// </value>
        public ChangeStatistics Overall
        {
            get { return _overall; }
        }

        #region Equality members


        protected bool Equals(ChangeSummary other)
        {
            return _index == other._index && Equals(_latest, other._latest) && Equals(_overall, other._overall);
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
            return Equals((ChangeSummary) obj);
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
                hashCode = (hashCode*397) ^ (_latest != null ? _latest.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (_overall != null ? _overall.GetHashCode() : 0);
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
            return string.Format("CurrentIndex: {0}, Latest Count: {1}, Overall Count: {2}", _index, _latest.Count,
                                 _overall.Count);
        }
    }
}