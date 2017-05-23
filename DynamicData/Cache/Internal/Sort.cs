using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal sealed class Sort<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly IComparer<TObject> _comparer;
        private readonly SortOptimisations _sortOptimisations;
        private readonly IObservable<IComparer<TObject>> _comparerChangedObservable;
        private readonly IObservable<Unit> _resorter;

        private readonly int _resetThreshold;

        public Sort(IObservable<IChangeSet<TObject, TKey>> source,
            IComparer<TObject> comparer,
            SortOptimisations sortOptimisations = SortOptimisations.None,
            IObservable<IComparer<TObject>> comparerChangedObservable = null,
            IObservable<Unit> resorter = null,
            int resetThreshold = -1)
        {
            if (comparer == null && comparerChangedObservable == null)
                throw new ArgumentException("Must specify comparer or comparerChangedObservable");

            _source = source ?? throw new ArgumentNullException(nameof(source));
            _comparer = comparer;
            _sortOptimisations = sortOptimisations;
            _resorter = resorter;
            _comparerChangedObservable = comparerChangedObservable;
            _resetThreshold = resetThreshold;
        }

        public IObservable<ISortedChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<ISortedChangeSet<TObject, TKey>>(observer =>
            {
                var sorter = new Sorter(_sortOptimisations, _comparer, _resetThreshold);
                var locker = new object();

                //check for nulls so we can prevent a lock when not required
                if (_comparerChangedObservable == null && _resorter == null)
                {
                    return _source
                        .Select(sorter.Sort)
                        .Where(result => result != null)
                        .SubscribeSafe(observer);
                }

                var comparerChanged = (_comparerChangedObservable ?? Observable.Never<IComparer<TObject>>())
                    .Synchronize(locker).Select(sorter.Sort);

                var sortAgain = (_resorter ?? Observable.Never<Unit>())
                    .Synchronize(locker).Select(_ => sorter.Sort());

                var dataChanged = _source.Synchronize(locker)
                    .Select(sorter.Sort);

                return comparerChanged
                    .Merge(dataChanged)
                    .Merge(sortAgain)
                    .Where(result => result != null)
                    .SubscribeSafe(observer);
            });
        }

        private class Sorter
        {
            private readonly ChangeAwareCache<TObject, TKey> _cache = new ChangeAwareCache<TObject, TKey>();
            private readonly SortOptimisations _optimisations;
            private readonly int _resetThreshold;

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
                _comparer = new KeyValueComparer<TObject, TKey>(comparer);
            }

            /// <summary>
            /// Sorts the specified changes. Will return null if there are no changes
            /// </summary>
            /// <param name="changes">The changes.</param>
            /// <returns></returns>
            public ISortedChangeSet<TObject, TKey> Sort(IChangeSet<TObject, TKey> changes)
            {
                return DoSort(SortReason.DataChanged, changes);
            }

            /// <summary>
            /// Sorts all data using the specified comparer
            /// </summary>
            /// <param name="comparer">The comparer.</param>
            /// <returns></returns>
            public ISortedChangeSet<TObject, TKey> Sort(IComparer<TObject> comparer)
            {
                _comparer = new KeyValueComparer<TObject, TKey>(comparer);
                return DoSort(SortReason.ComparerChanged);
            }

            /// <summary>
            /// Sorts all data using the current comparer
            /// </summary>
            /// <returns></returns>
            public ISortedChangeSet<TObject, TKey> Sort()
            {
                return DoSort(SortReason.Reorder);
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
                    _cache.Clone(changes);
                    changes = _cache.CaptureChanges();
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
                            changeSet = _calculator.ChangeComparer(_comparer);
                            if (_resetThreshold > 0 && _cache.Count >= _resetThreshold)
                            {
                                sortReason = SortReason.Reset;
                                _calculator.Reset(_cache);
                            }
                            else
                            {
                                sortReason = SortReason.Reorder;
                                changeSet =_calculator.Reorder();
                            }
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
}