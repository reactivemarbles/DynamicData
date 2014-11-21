using System;
using System.Collections.Generic;

namespace DynamicData.Operators
{
    internal sealed class Group<TObject, TKey, TGroupKey> : IGroup<TObject, TKey, TGroupKey>
    {
        private readonly TGroupKey _groupKey;
        private readonly IObservableCache<TObject, TKey> _cache;

        public Group(IObservableCache<TObject, TKey> updates, TGroupKey groupKey)
        {
            _cache = updates;
            _groupKey = groupKey;
        }

        public TGroupKey Key
        {
            get { return _groupKey; }
        }

        public IObservableCache<TObject, TKey> Cache
        {
            get { return _cache; }
        }

        #region Equality Members

        #region Equality members

        private bool Equals(Group<TObject, TKey, TGroupKey> other)
        {
            return EqualityComparer<TGroupKey>.Default.Equals(_groupKey, other._groupKey);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Group<TObject, TKey, TGroupKey> && Equals((Group<TObject, TKey, TGroupKey>) obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            return EqualityComparer<TGroupKey>.Default.GetHashCode(_groupKey);
        }

        #endregion

        #endregion

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return string.Format("Group: {0}", Key);
        }
    }
}