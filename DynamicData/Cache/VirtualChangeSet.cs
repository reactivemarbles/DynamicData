using System;
using System.Collections.Generic;
using DynamicData.Internal;

namespace DynamicData
{
    internal sealed class VirtualChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IVirtualChangeSet<TObject, TKey>, IEquatable<VirtualChangeSet<TObject, TKey>>
    {
        private readonly IKeyValueCollection<TObject, TKey> _sortedItems;
        private readonly IVirtualResponse _response;

        public VirtualChangeSet(IEnumerable<Change<TObject, TKey>> items, IKeyValueCollection<TObject, TKey> sortedItems, IVirtualResponse response)
            : base(items)
        {
            _sortedItems = sortedItems;
            _response = response;
        }

        private VirtualChangeSet()
            : base(ChangeSet<TObject, TKey>.Empty)
        {
            _sortedItems = new KeyValueCollection<TObject, TKey>();
        }

        #region IVirtualChangeSet<TObject,TKey> Members

        public IKeyValueCollection<TObject, TKey> SortedItems { get { return _sortedItems; } }

        public IVirtualResponse Response { get { return _response; } }

        #endregion

        #region Equality

        public bool Equals(VirtualChangeSet<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _response.Equals(other._response)
                   && Equals(_sortedItems, other._sortedItems);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((VirtualChangeSet<TObject, TKey>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = _response.GetHashCode();
                hashCode = (hashCode * 397) ^ (_sortedItems?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        public static bool operator ==(VirtualChangeSet<TObject, TKey> left, VirtualChangeSet<TObject, TKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(VirtualChangeSet<TObject, TKey> left, VirtualChangeSet<TObject, TKey> right)
        {
            return !Equals(left, right);
        }

        #endregion
    }
}
