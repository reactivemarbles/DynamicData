using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class ImmutableGroup<TObject,  TGroupKey> : IGrouping<TObject, TGroupKey>, IEquatable<ImmutableGroup<TObject,  TGroupKey>>
    {
        private readonly IReadOnlyCollection<TObject> _items;

        public TGroupKey Key { get; }

        internal ImmutableGroup(TGroupKey key, IList<TObject> items)
        {
            Key = key;

            var temp = new List<TObject>(items);
            _items = new ReadOnlyCollectionLight<TObject>(temp, temp.Count);
        }

        public int Count => _items.Count;
        public IEnumerable<TObject> Items => _items;

        #region Equality

        public bool Equals(ImmutableGroup<TObject, TGroupKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ImmutableGroup<TObject, TGroupKey> && Equals((ImmutableGroup<TObject, TGroupKey>) obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<TGroupKey>.Default.GetHashCode(Key);
        }

        public static bool operator ==(ImmutableGroup<TObject, TGroupKey> left, ImmutableGroup<TObject, TGroupKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ImmutableGroup<TObject, TGroupKey> left, ImmutableGroup<TObject, TGroupKey> right)
        {
            return !Equals(left, right);
        }

        #endregion

        public override string ToString()
        {
            return $"Grouping for: {Key} ({Count} items)";
        }
    }
}