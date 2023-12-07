// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class Sort<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IComparer<TObject>? _comparer;

    private readonly IObservable<IComparer<TObject>>? _comparerChangedObservable;

    private readonly int _resetThreshold;

    private readonly IObservable<Unit>? _resorter;

    private readonly SortOptimisations _sortOptimisations;

    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public Sort(IObservable<IChangeSet<TObject, TKey>> source, IComparer<TObject>? comparer, SortOptimisations sortOptimisations = SortOptimisations.None, IObservable<IComparer<TObject>>? comparerChangedObservable = null, IObservable<Unit>? resorter = null, int resetThreshold = -1)
    {
        if (comparer is null && comparerChangedObservable is null)
        {
            throw new ArgumentException("Must specify comparer or comparerChangedObservable");
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _comparer = comparer;
        _sortOptimisations = sortOptimisations;
        _resorter = resorter;
        _comparerChangedObservable = comparerChangedObservable;
        _resetThreshold = resetThreshold;
    }

    public IObservable<ISortedChangeSet<TObject, TKey>> Run() => Observable.Create<ISortedChangeSet<TObject, TKey>>(
            observer =>
            {
                var sorter = new Sorter(_sortOptimisations, _comparer, _resetThreshold);
                var locker = new object();

                // check for nulls so we can prevent a lock when not required
                if (_comparerChangedObservable is null && _resorter is null)
                {
                    return _source.Select(sorter.Sort).Where(result => result is not null).Select(x => x!).SubscribeSafe(observer);
                }

                var comparerChanged = (_comparerChangedObservable ?? Observable.Never<IComparer<TObject>>()).Synchronize(locker).Select(sorter.Sort);

                var sortAgain = (_resorter ?? Observable.Never<Unit>()).Synchronize(locker).Select(_ => sorter.Sort());

                var dataChanged = _source.Synchronize(locker).Select(sorter.Sort);

                return comparerChanged.Merge(dataChanged).Merge(sortAgain).Where(result => result is not null).Select(x => x!).SubscribeSafe(observer);
            });

    private sealed class Sorter(SortOptimisations optimisations, IComparer<TObject>? comparer = null, int resetThreshold = -1)
    {
        private readonly ChangeAwareCache<TObject, TKey> _cache = new();
        private IndexCalculator<TObject, TKey>? _calculator;

        private KeyValueComparer<TObject, TKey> _comparer = new(comparer);

        private bool _haveReceivedData;

        private bool _initialised;

        private IKeyValueCollection<TObject, TKey> _sorted = new KeyValueCollection<TObject, TKey>();

        /// <summary>
        /// Sorts the specified changes. Will return null if there are no changes.
        /// </summary>
        /// <param name="changes">The changes.</param>
        /// <returns>The sorted change set.</returns>
        public ISortedChangeSet<TObject, TKey>? Sort(IChangeSet<TObject, TKey> changes) => DoSort(SortReason.DataChanged, changes);

        /// <summary>
        /// Sorts all data using the specified comparer.
        /// </summary>
        /// <param name="comparer">The comparer.</param>
        /// <returns>The sorted change set.</returns>
        public ISortedChangeSet<TObject, TKey>? Sort(IComparer<TObject> comparer)
        {
            _comparer = new KeyValueComparer<TObject, TKey>(comparer);
            return DoSort(SortReason.ComparerChanged);
        }

        /// <summary>
        /// Sorts all data using the current comparer.
        /// </summary>
        /// <returns>The sorted change set.</returns>
        public ISortedChangeSet<TObject, TKey>? Sort() => DoSort(SortReason.Reorder);

        /// <summary>
        /// Sorts using the specified sorter. Will return null if there are no changes.
        /// </summary>
        /// <param name="sortReason">The sort reason.</param>
        /// <param name="changes">The changes.</param>
        /// <returns>The sorted change set.</returns>
        private SortedChangeSet<TObject, TKey>? DoSort(SortReason sortReason, IChangeSet<TObject, TKey>? changes = null)
        {
            if (changes is not null)
            {
                _cache.Clone(changes);
                changes = _cache.CaptureChanges();
                _haveReceivedData = true;
            }

            // if the comparer is not set, return nothing
            if (!_haveReceivedData)
            {
                return null;
            }

            if (!_initialised)
            {
                sortReason = SortReason.InitialLoad;
                _initialised = true;
            }
            else if (changes is not null && (resetThreshold > 0 && changes.Count >= resetThreshold))
            {
                sortReason = SortReason.Reset;
            }

            IChangeSet<TObject, TKey>? changeSet;
            switch (sortReason)
            {
                case SortReason.InitialLoad:
                    {
                        // For the first batch, changes may have arrived before the comparer was set.
                        // therefore infer the first batch of changes from the cache
                        _calculator = new IndexCalculator<TObject, TKey>(_comparer, optimisations);
                        changeSet = _calculator.Load(_cache);
                    }

                    break;

                case SortReason.Reset:
                    {
                        if (_calculator is null)
                        {
                            throw new InvalidOperationException("The calculator has not been initialized");
                        }

                        _calculator?.Reset(_cache);
                        changeSet = changes;
                    }

                    break;

                case SortReason.DataChanged:
                    {
                        if (_calculator is null)
                        {
                            throw new InvalidOperationException("The calculator has not been initialized");
                        }

                        if (changes is null)
                        {
                            throw new InvalidOperationException("Data has been indicated as changed, but changes is null.");
                        }

                        changeSet = _calculator.Calculate(changes);
                    }

                    break;

                case SortReason.ComparerChanged:
                    {
                        if (_calculator is null)
                        {
                            throw new InvalidOperationException("The calculator has not been initialized");
                        }

                        changeSet = _calculator.ChangeComparer(_comparer);
                        if (resetThreshold > 0 && _cache.Count >= resetThreshold)
                        {
                            sortReason = SortReason.Reset;
                            _calculator.Reset(_cache);
                        }
                        else
                        {
                            sortReason = SortReason.Reorder;
                            changeSet = _calculator.Reorder();
                        }
                    }

                    break;

                case SortReason.Reorder:
                    {
                        if (_calculator is null)
                        {
                            throw new InvalidOperationException("The calculator has not been initialized");
                        }

                        changeSet = _calculator.Reorder();
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sortReason));
            }

            if (changeSet is null)
            {
                throw new InvalidOperationException("Change set must not be null.");
            }

            if (_calculator is null)
            {
                throw new InvalidOperationException("The calculator has not been initialized");
            }

            if (sortReason == SortReason.Reorder && changeSet.Count == 0)
            {
                return null;
            }

            _sorted = new KeyValueCollection<TObject, TKey>(_calculator.List.ToList(), _comparer, sortReason, optimisations);
            return new SortedChangeSet<TObject, TKey>(_sorted, changeSet);
        }
    }
}
