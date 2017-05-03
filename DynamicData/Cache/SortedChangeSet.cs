using System.Collections.Generic;
using System.Linq;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal class SortedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, ISortedChangeSet<TObject, TKey>
    {
        public new static readonly ISortedChangeSet<TObject, TKey> Empty = new SortedChangeSet<TObject, TKey>();
        
        public IKeyValueCollection<TObject, TKey> SortedItems { get; }


        public SortedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates)
            : base(updates)
        {
            SortedItems = sortedItems;
        }

        private SortedChangeSet()
        {
            SortedItems = new KeyValueCollection<TObject, TKey>();
        }


        #region Equality Members

        public bool Equals(SortedChangeSet<TObject, TKey> other)
        {
            return SortedItems.SequenceEqual(other.SortedItems);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((SortedChangeSet<TObject, TKey>)obj);
        }

        public override int GetHashCode()
        {
            return SortedItems?.GetHashCode() ?? 0;
        }

        #endregion

        public override string ToString()
        {
            return $"SortedChangeSet. Count= {SortedItems.Count}. Updates = {Count}";
        }
    }
}
