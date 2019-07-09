using System;
using System.Collections.Generic;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal sealed class VirtualChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IVirtualChangeSet<TObject, TKey>, IEquatable<VirtualChangeSet<TObject, TKey>>
    {
        public new static readonly IVirtualChangeSet<TObject, TKey> Empty = new VirtualChangeSet<TObject, TKey>();


        public IKeyValueCollection<TObject, TKey> SortedItems { get; }
        public IVirtualResponse Response { get; }

        public VirtualChangeSet(IEnumerable<Change<TObject, TKey>> items, IKeyValueCollection<TObject, TKey> sortedItems, IVirtualResponse response)
            : base(items)
        {
            SortedItems = sortedItems;
            Response = response;
        }

        private VirtualChangeSet()
        {
            SortedItems = new KeyValueCollection<TObject, TKey>();
            Response = new VirtualResponse(0,0,0);
        }


        #region Equality

        public bool Equals(VirtualChangeSet<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Response.Equals(other.Response)
                   && Equals(SortedItems, other.SortedItems);
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
                int hashCode = Response.GetHashCode();
                hashCode = (hashCode * 397) ^ (SortedItems?.GetHashCode() ?? 0);
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
