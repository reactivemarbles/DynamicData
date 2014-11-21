using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal sealed class PagedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IPagedChangeSet<TObject, TKey>
    {
        private readonly IKeyValueCollection<TObject, TKey> _sortedItems;
        private readonly IPageResponse _response;

        public PagedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates, IPageResponse response)
            : base(updates)
        {
            _response = response;
            _sortedItems = sortedItems;
        }

        public IKeyValueCollection<TObject, TKey> SortedItems
        {
            get { return _sortedItems; }
        }

        public IPageResponse Response
        {
            get { return _response; }
        }


        #region Equality Members

        protected bool Equals(PagedChangeSet<TObject, TKey> other)
        {
            return SortedItems.SequenceEqual(other.SortedItems);
            // return Equals(this.SortedItems, other.SortedItems);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((PagedChangeSet<TObject, TKey>)obj);
        }

        public override int GetHashCode()
        {
            return (SortedItems != null ? SortedItems.GetHashCode() : 0);
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the SortedItems <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the SortedItems <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0}, Response: {1}, SortedItems: {2}", base.ToString(), _response, _sortedItems);
        }
    }
}