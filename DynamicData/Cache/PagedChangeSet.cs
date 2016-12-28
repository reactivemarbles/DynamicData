using System.Collections.Generic;
using System.Linq;
using DynamicData.Operators;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal sealed class PagedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IPagedChangeSet<TObject, TKey>
    {
        public PagedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates, IPageResponse response)
            : base(updates)
        {
            Response = response;
            SortedItems = sortedItems;
        }

        public IKeyValueCollection<TObject, TKey> SortedItems { get; }
        public IPageResponse Response { get; }

        #region Equality Members

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(PagedChangeSet<TObject, TKey> other)
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
            return SortedItems?.GetHashCode() ?? 0;
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
            return string.Format("{0}, Response: {1}, SortedItems: {2}", base.ToString(), Response, SortedItems);
        }
    }
}
