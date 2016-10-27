using System.Collections.Generic;
using System.Linq;


namespace DynamicData.Cache.Internal
{
    /// <summary>
    /// Calculates a sequential change set.
    /// 
    /// This enables the binding infrastructure to simply iterate the change set
    /// and apply indexed changes with no need to apply ant expensive IndexOf() operations.
    /// </summary>
    internal sealed class IndexCalculator<TObject, TKey> : IIndexCalculator<TObject, TKey>
    {
        private KeyValueComparer<TObject, TKey> _comparer;
        private List<KeyValuePair<TKey, TObject>> _list;

        private readonly SortOptimisations _optimisations;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public IndexCalculator(KeyValueComparer<TObject, TKey> comparer, SortOptimisations optimisations)
        {
            _comparer = comparer;
            _optimisations = optimisations;
            _list = new List<KeyValuePair<TKey, TObject>>();
        }

        /// <summary>
        /// Initialises the specified changes.
        /// </summary>
        /// <param name="cache">The cache.</param>
        /// <returns></returns>
        public IChangeSet<TObject, TKey> Load(ChangeAwareCache<TObject, TKey> cache)
        {
            //for the first batch of changes may have arrived before the comparer was set.
            //therefore infer the first batch of changes from the cache
            _list = cache.KeyValues.OrderBy(kv => kv, _comparer).ToList();
            var initialItems = _list.Select((t, index) => new Change<TObject, TKey>(ChangeReason.Add, t.Key, t.Value, index));
            return new ChangeSet<TObject, TKey>(initialItems);
        }

        /// <summary>
        /// Initialises the specified changes.
        /// </summary>
        /// <param name="cache">The cache.</param>
        /// <returns></returns>
        public void Reset(ChangeAwareCache<TObject, TKey> cache)
        {
            _list = cache.KeyValues.OrderBy(kv => kv, _comparer).ToList();
        }

        public IChangeSet<TObject, TKey> ChangeComparer(KeyValueComparer<TObject, TKey> comparer)
        {
            _comparer = comparer;
            _list = _list.OrderBy(kv => kv, _comparer).ToList();
            return ChangeSet<TObject, TKey>.Empty;
        }

        public IChangeSet<TObject, TKey> Reorder()
        {
            var result = new List<Change<TObject, TKey>>();

            if (_optimisations.HasFlag(SortOptimisations.IgnoreEvaluates))
            {
                //reorder entire sequence and do not calculate moves
                _list = _list.OrderBy(kv => kv, _comparer).ToList();
            }
            else
            {
                int index = -1;
                var sorted = _list.OrderBy(t => t, _comparer).ToList();
                foreach (var item in sorted)
                {
                    KeyValuePair<TKey, TObject> current = item;
                    index++;

                    //Cannot use binary search as Resort is implicit of a mutable change
                    KeyValuePair<TKey, TObject> existing = _list[index];
                    var areequal = EqualityComparer<TKey>.Default.Equals(current.Key, existing.Key);
                    if (areequal)
                    {
                        continue;
                    }
                    var old = _list.IndexOf(current);
                    _list.RemoveAt(old);
                    _list.Insert(index, current);

                    result.Add(new Change<TObject, TKey>(current.Key, current.Value, index, old));
                }
            }

            return new ChangeSet<TObject, TKey>(result);
        }

        /// <summary>
        /// Dynamic calculation of moved items which produce a result which can be enumerated through in order
        /// </summary>
        /// <returns></returns>
        public IChangeSet<TObject, TKey> Calculate(IChangeSet<TObject, TKey> changes)
        {
            var result = new List<Change<TObject, TKey>>();

            //  var notEvaluates = changes.Where(c => c.Reason != ChangeReason.Evaluate).ToList();

            foreach (var u in changes)
            {
                var current = new KeyValuePair<TKey, TObject>(u.Key, u.Current);

                switch (u.Reason)
                {
                    case ChangeReason.Add:
                        {
                            var position = GetInsertPositionBinary(current);
                            _list.Insert(position, current);

                            result.Add(new Change<TObject, TKey>(ChangeReason.Add, u.Key, u.Current, position));
                        }
                        break;

                    case ChangeReason.Update:
                        {
                            var previous = new KeyValuePair<TKey, TObject>(u.Key, u.Previous.Value);
                            var old = GetCurrentPosition(previous);
                            _list.RemoveAt(old);

                            var newposition = GetInsertPositionBinary(current);
                            _list.Insert(newposition, current);

                            result.Add(new Change<TObject, TKey>(ChangeReason.Update,
                                                                 u.Key,
                                                                 u.Current, u.Previous, newposition, old));
                        }
                        break;

                    case ChangeReason.Remove:
                        {
                            var position = GetCurrentPosition(current);
                            _list.RemoveAt(position);
                            result.Add(new Change<TObject, TKey>(ChangeReason.Remove, u.Key, u.Current, position));
                        }
                        break;

                    case ChangeReason.Evaluate:
                        {
                            result.Add(u);
                        }
                        break;
                    default:
                        break;
                }
            }

            //for evaluates, check whether the change forces a new position
            var evaluates = changes.Where(c => c.Reason == ChangeReason.Evaluate)
                                   .OrderByDescending(x => new KeyValuePair<TKey, TObject>(x.Key, x.Current), _comparer)
                                   .ToList();

            if (evaluates.Count != 0 && _optimisations.HasFlag(SortOptimisations.IgnoreEvaluates))
            {
                //reorder entire sequence and do not calculate moves
                _list = _list.OrderBy(kv => kv, _comparer).ToList();
            }
            else
            {
                //calculate moves.  Very expensive operation
                //TODO: Try and make this better
                foreach (var u in evaluates)
                {
                    var current = new KeyValuePair<TKey, TObject>(u.Key, u.Current);
                    var old = _list.IndexOf(current);
                    if (old == -1) continue;

                    int newposition = GetInsertPositionLinear(_list, current);

                    if (old < newposition)
                    {
                        newposition--;
                    }

                    if (old == newposition)
                    {
                        continue;
                    }

                    _list.RemoveAt(old);
                    _list.Insert(newposition, current);
                    result.Add(new Change<TObject, TKey>(u.Key, u.Current, newposition, old));
                }
            }

            return new ChangeSet<TObject, TKey>(result);
        }

        public IComparer<KeyValuePair<TKey, TObject>> Comparer => _comparer;

        public List<KeyValuePair<TKey, TObject>> List => _list;

        private int GetCurrentPosition(KeyValuePair<TKey, TObject> item)
        {
            int index;

            if (_optimisations.HasFlag(SortOptimisations.ComparesImmutableValuesOnly))
            {
                index = _list.BinarySearch(item, _comparer);

                if (index < 0)
                    throw new SortException("Current position cannot be found.  Ensure the comparer includes a unique value, or do not specify ComparesImmutableValuesOnly");
            }
            else
            {
                index = _list.IndexOf(item);

                if (index < 0)
                    throw new SortException("Current position cannot be found. The item is not in the collection");
            }
            return index;
        }

        private int GetInsertPositionLinear(IList<KeyValuePair<TKey, TObject>> list, KeyValuePair<TKey, TObject> item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (_comparer.Compare(item, list[i]) < 0)
                {
                    return i;
                }
            }
            return _list.Count;
        }

        private int GetInsertPositionBinary(KeyValuePair<TKey, TObject> item)
        {
            int index = _list.BinarySearch(item, _comparer);

            if (index > 0)
            {
                var indx = (int)index;
                index = _list.BinarySearch(indx - 1, _list.Count - indx, item, _comparer);
                if (index > 0)
                {
                    return indx;
                }
            }

            int insertIndex = ~index;
            return insertIndex;
        }
    }
}
