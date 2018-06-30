using DynamicData.Kernel;
using System;
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
    internal sealed class IndexCalculator<TObject, TKey>
    {
        private KeyValueComparer<TObject, TKey> _comparer;
        private LinkedList<KeyValuePair<TKey, TObject>> _list;

        private readonly SortOptimisations _optimisations;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public IndexCalculator(KeyValueComparer<TObject, TKey> comparer, SortOptimisations optimisations)
        {
            _comparer = comparer;
            _optimisations = optimisations;
            _list = new LinkedList<KeyValuePair<TKey, TObject>>();
        }

        public IEnumerable<Change<TObject, TKey>> ReduceChanges(IEnumerable<Change<TObject, TKey>> input)
        {
            return input
                .GroupBy(kvp=>kvp.Key)
                .Select(g => {
                    return g.Aggregate(Optional<Change<TObject, TKey>>.None, (acc, kvp) => ChangesReducer.Reduce(acc, kvp));
                })
                .Where(x=>x.HasValue)
                .Select(x=>x.Value);
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
            _list = new LinkedList<KeyValuePair<TKey, TObject>>(cache.KeyValues.OrderBy(kv => kv, _comparer));
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
            _list = new LinkedList<KeyValuePair<TKey, TObject>>(cache.KeyValues.OrderBy(kv => kv, _comparer));
        }

        public IChangeSet<TObject, TKey> ChangeComparer(KeyValueComparer<TObject, TKey> comparer)
        {
            _comparer = comparer;
            return ChangeSet<TObject, TKey>.Empty;
        }

        public IChangeSet<TObject, TKey> Reorder()
        {
            var result = new List<Change<TObject, TKey>>();

            if (_optimisations.HasFlag(SortOptimisations.IgnoreEvaluates))
            {
                //reorder entire sequence and do not calculate moves
                _list = new LinkedList<KeyValuePair<TKey, TObject>>(_list.OrderBy(kv => kv, _comparer));
            }
            else
            {
                var sorted = _list.OrderBy(t => t, _comparer).Select((item, i) => Tuple.Create(item, i)).ToList();
                var oldByKey = _list.Select((item, i) => Tuple.Create(item, i)).ToDictionary(x => x.Item1.Key, x => x.Item2);

                foreach (var item in sorted)
                {
                    var currentItem = item.Item1;
                    var currentIndex = item.Item2;

                    var previousIndex = oldByKey[currentItem.Key];

                    if (currentIndex != previousIndex)
                    {
                        result.Add(new Change<TObject, TKey>(currentItem.Key, currentItem.Value, currentIndex, previousIndex));
                    }
                }

                _list = new LinkedList<KeyValuePair<TKey, TObject>>(sorted.Select(s => s.Item1));
            }

            return new ChangeSet<TObject, TKey>(result);
        }

        /// <summary>
        /// Dynamic calculation of moved items which produce a result which can be enumerated through in order
        /// </summary>
        /// <returns></returns>
        public IChangeSet<TObject, TKey> Calculate(IChangeSet<TObject, TKey> changes)
        {
            var reducedChanges = ReduceChanges(changes).ToList();
            var changesByReason = reducedChanges.GroupBy(c => c.Reason).ToDictionary(x => x.Key, x => x.AsEnumerable());

            var result = new List<Change<TObject, TKey>>(reducedChanges.Count);

            var refreshes = changesByReason.GetOrEmpty(ChangeReason.Refresh);
            var removals = changesByReason.GetOrEmpty(ChangeReason.Remove);
            var updateChanges = changesByReason.GetOrEmpty(ChangeReason.Update).Concat(refreshes).OrderBy(r => new KeyValuePair<TKey, TObject>(r.Key, r.Current), _comparer);

            var keysToBeRemoved = updateChanges.Concat(removals).ToDictionary(x => x.Key, x => x);
            var updatePreviousValues = new Dictionary<KeyValuePair<TKey, TObject>, IndexAndNode<KeyValuePair<TKey, TObject>>>();

            if (keysToBeRemoved.Any())
            {
                var index = 0;
                var node = _list.First;
                while (node != null)
                {
                    if (keysToBeRemoved.ContainsKey(node.Value.Key))
                    {
                        var toBeRemoved = keysToBeRemoved[node.Value.Key];

                        var nodeCopy = node;
                        if(toBeRemoved.Reason == ChangeReason.Remove)
                        {
                            result.Add(new Change<TObject, TKey>(ChangeReason.Remove, toBeRemoved.Key, toBeRemoved.Current, index));
                        } else
                        {
                            var kvp = new KeyValuePair<TKey, TObject>(node.Value.Key, toBeRemoved.Current);
                            updatePreviousValues[kvp] = IndexAndNode.Create(index, node);
                            index++;
                        }

                        node = node.Next;
                        _list.Remove(nodeCopy);
                        keysToBeRemoved.Remove(nodeCopy.Value.Key);

                        if (!keysToBeRemoved.Any()) break;
                    }
                    else {
                        node = node.Next;
                        index++;
                    }
               }
            }

            var updates = new Queue<Change<TObject, TKey>>(updateChanges);
            if (updates.Any())
            {
                var index = -1;
                var node = _list.First;

                var alreadyAddedNodes = new SortedSet<KeyValuePair<TKey, TObject>>(_comparer);
                var nodeToBeUpdated = updates.Peek();
                var kvp = new KeyValuePair<TKey, TObject>(nodeToBeUpdated.Key, nodeToBeUpdated.Current);
                while (node != null)
                {
                    index++;
                    var shouldInsertElement = _comparer.Compare(node.Value, kvp) >= 0;
                    if (shouldInsertElement)
                    {
                        var previous = updatePreviousValues[kvp];

                        // Previous node index is shifter by count of already added nodes
                        // which are less than the one we are about to insert
                        var indexOffset = alreadyAddedNodes.TakeWhile(kv => _comparer.Compare(kvp, node.Value) < 0).Count();

                        var previousIndex = previous.Index + indexOffset;
                        var previousValue = previous.Node.Value;

                        var nodeToAdd = new LinkedListNode<KeyValuePair<TKey, TObject>>(kvp);
                        _list.AddBefore(node, nodeToAdd);

                        if(previousIndex == index)
                        {
                            result.Add(new Change<TObject, TKey>(nodeToBeUpdated.Reason, kvp.Key, kvp.Value, previousValue.Value, index, previousIndex));
                        } else
                        {
                            alreadyAddedNodes.Add(kvp);
                            result.Add(new Change<TObject, TKey>(kvp.Key, kvp.Value, index, previousIndex));
                            result.Add(new Change<TObject, TKey>(nodeToBeUpdated.Reason, kvp.Key, kvp.Value, previousValue.Value, index, index));
                        }

                        node = nodeToAdd;
                        updates.Dequeue();
                        if (!updates.Any()) break;
                        
                        nodeToBeUpdated = updates.Peek();
                        kvp = new KeyValuePair<TKey, TObject>(nodeToBeUpdated.Key, nodeToBeUpdated.Current);
                    } 
                    node = node.Next;
                }

                if(updates.Any())
                {
                    var previous = updatePreviousValues[kvp];
                    var previousIndex = previous.Index;
                    var previousValue = previous.Node.Value;

                    if(index == -1)
                    {
                        result.Add(new Change<TObject, TKey>(nodeToBeUpdated.Reason, kvp.Key, kvp.Value, previousValue.Value, 0, 0));
                    } else
                    {
                        result.Add(new Change<TObject, TKey>(kvp.Key, kvp.Value, index, previousIndex));
                        result.Add(new Change<TObject, TKey>(nodeToBeUpdated.Reason, kvp.Key, kvp.Value, previousValue.Value, index, index));
                    }

                    _list.AddLast(kvp);
                }
            }

            var adds = new Queue<Change<TObject, TKey>>(changesByReason.GetOrEmpty(ChangeReason.Add).OrderBy(r => r.CurrentIndex));
            if (adds.Any())
            {
                var index = -1;
                var node = _list.First;
                var nodeToBeAdded = adds.Peek();
                var kvp = new KeyValuePair<TKey, TObject>(nodeToBeAdded.Key, nodeToBeAdded.Current);
                while (node != null)
                {
                    index++;
                    var shouldInsert = _comparer.Compare(node.Value, kvp) > 0;
                    if (shouldInsert)
                    {
                        var nodeToAdd = new LinkedListNode<KeyValuePair<TKey, TObject>>(kvp);
                        _list.AddBefore(node, nodeToAdd);
                        result.Add(new Change<TObject, TKey>(ChangeReason.Add, nodeToBeAdded.Key, nodeToBeAdded.Current, index));

                        node = nodeToAdd;

                        adds.Dequeue();
                        if (!adds.Any()) break;
                        
                        nodeToBeAdded = adds.Peek();
                        kvp = new KeyValuePair<TKey, TObject>(nodeToBeAdded.Key, nodeToBeAdded.Current);
                    }
                    node = node.Next;
                }

                if(adds.Any())
                {
                    _list.AddLast(kvp);
                    result.Add(new Change<TObject, TKey>(ChangeReason.Add, nodeToBeAdded.Key, nodeToBeAdded.Current, index));
                }
            }

            return new ChangeSet<TObject, TKey>(result);
        }

        public IComparer<KeyValuePair<TKey, TObject>> Comparer => _comparer;

        public LinkedList<KeyValuePair<TKey, TObject>> List => _list;

     
    }
}
