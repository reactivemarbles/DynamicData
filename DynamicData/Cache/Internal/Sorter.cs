using System;
using System.Collections.Generic;
using System.Diagnostics;
using DynamicData.Operators;

namespace DynamicData.Internal
{
    internal class Sorter<TObject, TKey>
    {
        private readonly Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();
        private readonly IntermediateUpdater<TObject, TKey> _updater;
        private readonly SortOptimisations _optimisations;
        private readonly int _resetThreshold;
        private readonly object _locker = new object();

        private KeyValueComparer<TObject, TKey> _comparer;
        private IKeyValueCollection<TObject, TKey> _sorted = new KeyValueCollection<TObject, TKey>();
        private bool _haveReceivedData = false;
        private bool _initialised = false;
        private IndexCalculator<TObject, TKey> _calculator;

        public Sorter(SortOptimisations optimisations,
                      IComparer<TObject> comparer = null,
                      int resetThreshold = -1)
        {
            _optimisations = optimisations;
            _resetThreshold = resetThreshold;
            _updater = new IntermediateUpdater<TObject, TKey>(_cache);
            _comparer = new KeyValueComparer<TObject, TKey>(comparer);
        }

        /// <summary>
        /// Sorts the specified changes. Will return null if there are no changes
        /// </summary>
        /// <param name="changes">The changes.</param>
        /// <returns></returns>
        public ISortedChangeSet<TObject, TKey> Sort(IChangeSet<TObject, TKey> changes)
        {
            lock (_locker)
            {
                return DoSort(SortReason.DataChanged, changes);
            }
        }

        /// <summary>
        /// Sorts all data using the specified comparer
        /// </summary>
        /// <param name="comparer">The comparer.</param>
        /// <returns></returns>
        public ISortedChangeSet<TObject, TKey> Sort(IComparer<TObject> comparer)
        {
            lock (_locker)
            {
                _comparer = new KeyValueComparer<TObject, TKey>(comparer);
                return DoSort(SortReason.ComparerChanged);
            }
        }

        /// <summary>
        /// Sorts all data using the current comparer
        /// </summary>
        /// <returns></returns>
        public ISortedChangeSet<TObject, TKey> Sort()
        {
            lock (_locker)
            {
                return DoSort(SortReason.Reorder);
            }
        }

        /// <summary>
        /// Sorts using the specified sorter. Will return null if there are no changes
        /// </summary>
        /// <param name="sortReason">The sort reason.</param>
        /// <param name="changes">The changes.</param>
        /// <returns></returns>
        private ISortedChangeSet<TObject, TKey> DoSort(SortReason sortReason, IChangeSet<TObject, TKey> changes = null)
        {
            if (changes != null)
            {
                _updater.Update(changes);
                changes = _updater.AsChangeSet();
                _haveReceivedData = true;
                if (_comparer == null)
                    return null;
            }

            //if the comparer is not set, return nothing
            if (_comparer == null || !_haveReceivedData)
            {
                return null;
            }

            if (!_initialised)
            {
                sortReason = SortReason.InitialLoad;
                _initialised = true;
            }
            else if (changes != null && (_resetThreshold > 0 && changes.Count >= _resetThreshold))
            {
                sortReason = SortReason.Reset;
            }

            //TODO: Create a sorted cache (could create an sorted observable list perhaps?)
            IChangeSet<TObject, TKey> changeSet;
            switch (sortReason)
            {
                case SortReason.InitialLoad:
                {
                    //For the first batch, changes may have arrived before the comparer was set.
                    //therefore infer the first batch of changes from the cache
                    _calculator = new IndexCalculator<TObject, TKey>(_comparer, _optimisations);
                    changeSet = _calculator.Load(_cache);
                }
                    break;
                case SortReason.Reset:
                {
                    _calculator.Reset(_cache);
                    changeSet = changes;
                }
                    break;
                case SortReason.DataChanged:
                {
                    changeSet = _calculator.Calculate(changes);
                }
                    break;

                case SortReason.ComparerChanged:
                {
                    sortReason = SortReason.ComparerChanged;
                    changeSet = _calculator.ChangeComparer(_comparer);
                }
                    break;

                case SortReason.Reorder:
                {
                    changeSet = _calculator.Reorder();
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sortReason));
            }

            Debug.Assert(changeSet != null, "changeSet != null");
            if ((sortReason == SortReason.InitialLoad || sortReason == SortReason.DataChanged)
                && changeSet.Count == 0)
            {
                return null;
            }

            if (sortReason == SortReason.Reorder && changeSet.Count == 0) return null;

            _sorted = new KeyValueCollection<TObject, TKey>(_calculator.List, _comparer, sortReason, _optimisations);
            return new SortedChangeSet<TObject, TKey>(_sorted, changeSet);
        }
    }
}
