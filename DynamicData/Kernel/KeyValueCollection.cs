using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Operators;

namespace DynamicData.Kernel
{
    internal class KeyValueCollection<TObject, TKey> : IKeyValueCollection<TObject, TKey>
    {
        private readonly KeyComparer<TObject, TKey> _keyComparer = new KeyComparer<TObject, TKey>();
        private readonly List<KeyValuePair<TKey,TObject>> _items;
        private readonly IComparer<KeyValuePair<TKey,TObject>> _comparer;
        private readonly SortReason _sortReason;
        private readonly SortOptimisations _optimisations;


        public KeyValueCollection(IEnumerable<KeyValuePair<TKey,TObject>> items, 
            IComparer<KeyValuePair<TKey,TObject>> comparer, 
            SortReason sortReason, SortOptimisations optimisations)
        {
            if (items == null) throw new ArgumentNullException("items");
            _items = items.ToList();
            _comparer = comparer;
            _sortReason = sortReason;
            _optimisations = optimisations;
        }

        public KeyValueCollection()
        {
            _optimisations = SortOptimisations.None;
            _items = new List<KeyValuePair<TKey,TObject>>();
            _comparer = new KeyValueComparer<TObject, TKey>();
        }


        /// <summary>
        /// Gets the comparer used to peform the sort
        /// </summary>
        /// <value>
        /// The comparer.
        /// </value>
        public IComparer<KeyValuePair<TKey,TObject>> Comparer
        {
            get { return _comparer; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public KeyValuePair<TKey,TObject> this[int index]
        {
            get { return _items[index]; }
        }

        public SortReason SortReason
        {
            get { return _sortReason; }
        }

        public SortOptimisations Optimisations
        {
            get { return _optimisations; }
        }


        public IEnumerator<KeyValuePair<TKey,TObject>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}