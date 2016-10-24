using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Internal;
using DynamicData.Kernel;
using DynamicData.Operators;

namespace DynamicData.Cache.Internal
{
    internal class KeyValueCollection<TObject, TKey> : IKeyValueCollection<TObject, TKey>
    {
        private readonly List<KeyValuePair<TKey, TObject>> _items;

        public KeyValueCollection(IEnumerable<KeyValuePair<TKey, TObject>> items,
                                  IComparer<KeyValuePair<TKey, TObject>> comparer,
                                  SortReason sortReason,
                                  SortOptimisations optimisations)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items = items.AsList();
            Comparer = comparer;
            SortReason = sortReason;
            Optimisations = optimisations;
        }

        public KeyValueCollection()
        {
            Optimisations = SortOptimisations.None;
            _items = new List<KeyValuePair<TKey, TObject>>();
            Comparer = new KeyValueComparer<TObject, TKey>();
        }

        /// <summary>
        /// Gets the comparer used to peform the sort
        /// </summary>
        /// <value>
        /// The comparer.
        /// </value>
        public IComparer<KeyValuePair<TKey, TObject>> Comparer { get; }

        public int Count => _items.Count;

        public KeyValuePair<TKey, TObject> this[int index] => _items[index];

        public SortReason SortReason { get; }

        public SortOptimisations Optimisations { get; }

        public IEnumerator<KeyValuePair<TKey, TObject>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
