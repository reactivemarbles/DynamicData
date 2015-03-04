using System.Collections.Generic;
using System.Linq;

namespace DynamicData
{
    internal class SortedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>,ISortedChangeSet<TObject, TKey>
    {
        private readonly IKeyValueCollection<TObject, TKey> _sortedItems;

        public SortedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates)
            : base(updates)
        {
            _sortedItems = sortedItems;
        }

        public IKeyValueCollection<TObject, TKey> SortedItems
        {
            get { return _sortedItems; }
        }


        #region Equality Members

        protected bool Equals(SortedChangeSet<TObject, TKey> other)
        {
            return SortedItems.SequenceEqual(other.SortedItems);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((SortedChangeSet<TObject, TKey>) obj);
        }

        public override int GetHashCode()
        {
            return (SortedItems != null ? SortedItems.GetHashCode() : 0);
        }

        #endregion

        public override string ToString()
        {
            return string.Format("SortedChangeSet. Count= {0}. Updates = {1}", SortedItems.Count, Count);
        }
    }
}