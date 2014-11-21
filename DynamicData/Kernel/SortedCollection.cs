using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Operators;

namespace DynamicData.Kernel
{
    internal class SortedCollection<T> : ISortedCollection<T>
    {
        private readonly IList<T> _items;
        private readonly IComparer<T> _comparer;
        private readonly SortReason _sortReason;

        public SortedCollection(IEnumerable<T> items, IComparer<T> comparer, SortReason sortReason)
        {
            if (items == null) throw new ArgumentNullException("items");
            _items = items.ToList();
            _comparer = comparer;
            _sortReason = sortReason;
        }

        public SortedCollection()
        {
            _items =new List<T>();
            _comparer = new UniqueComparer<T>();
        }

        /// <summary>
        /// Gets the comparer used to peform the sort
        /// </summary>
        /// <value>
        /// The comparer.
        /// </value>
        public IComparer<T> Comparer
        {
            get { return _comparer; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public T this[int index]
        {
            get { return _items[index]; }
        }

        public SortReason SortReason
        {
            get { return _sortReason; }
        }


        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


}