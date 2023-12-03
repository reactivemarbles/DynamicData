// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// <para>Calculates a sequential change set.</para>
/// <para>
/// This enables the binding infrastructure to simply iterate the change set
/// and apply indexed changes with no need to apply ant expensive IndexOf() operations.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="IndexCalculator{TObject, TKey}"/> class.
/// </remarks>
/// <param name="comparer">The comparer to use.</param>
/// <param name="optimisations">Selected indexing optimisations.</param>
internal sealed class IndexCalculator<TObject, TKey>(KeyValueComparer<TObject, TKey> comparer, SortOptimisations optimisations)
    where TObject : notnull
    where TKey : notnull
{
    private KeyValueComparer<TObject, TKey> _comparer = comparer;

    public IComparer<KeyValuePair<TKey, TObject>> Comparer => _comparer;

    public List<KeyValuePair<TKey, TObject>> List { get; private set; } = [];

    /// <summary>
    /// Dynamic calculation of moved items which produce a result which can be enumerated through in order.
    /// </summary>
    /// <param name="changes">The change set.</param>
    /// <returns>A change set with the calculations.</returns>
    public IChangeSet<TObject, TKey> Calculate(IChangeSet<TObject, TKey> changes)
    {
        var result = new List<Change<TObject, TKey>>(changes.Count);
        var refreshes = new List<Change<TObject, TKey>>(changes.Refreshes);

        foreach (var u in changes.ToConcreteType())
        {
            var current = new KeyValuePair<TKey, TObject>(u.Key, u.Current);

            switch (u.Reason)
            {
                case ChangeReason.Add:
                    {
                        var position = GetInsertPositionBinary(current);
                        List.Insert(position, current);

                        result.Add(new Change<TObject, TKey>(ChangeReason.Add, u.Key, u.Current, position));
                    }

                    break;

                case ChangeReason.Update:
                    {
                        var previous = new KeyValuePair<TKey, TObject>(u.Key, u.Previous.Value);
                        var old = GetCurrentPosition(previous);
                        List.RemoveAt(old);

                        var newPosition = GetInsertPositionBinary(current);
                        List.Insert(newPosition, current);

                        result.Add(new Change<TObject, TKey>(ChangeReason.Update, u.Key, u.Current, u.Previous, newPosition, old));
                    }

                    break;

                case ChangeReason.Remove:
                    {
                        var position = GetCurrentPosition(current);
                        List.RemoveAt(position);
                        result.Add(new Change<TObject, TKey>(ChangeReason.Remove, u.Key, u.Current, position));
                    }

                    break;

                case ChangeReason.Refresh:
                    {
                        refreshes.Add(u);
                        result.Add(u);
                    }

                    break;
            }
        }

        // for evaluates, check whether the change forces a new position
        var evaluates = refreshes.OrderByDescending(x => new KeyValuePair<TKey, TObject>(x.Key, x.Current), _comparer).ToList();

        if (evaluates.Count != 0 && optimisations.HasFlag(SortOptimisations.IgnoreEvaluates))
        {
            // reorder entire sequence and do not calculate moves
            List = [.. List.OrderBy(kv => kv, _comparer)];
        }
        else
        {
            // calculate moves.  Very expensive operation
            // TODO: Try and make this better
            foreach (var u in evaluates)
            {
                var current = new KeyValuePair<TKey, TObject>(u.Key, u.Current);
                var old = List.IndexOf(current);
                if (old == -1)
                {
                    continue;
                }

                var newPosition = GetInsertPositionLinear(List, current);

                if (old < newPosition)
                {
                    newPosition--;
                }

                if (old == newPosition)
                {
                    continue;
                }

                List.RemoveAt(old);
                List.Insert(newPosition, current);
                result.Add(new Change<TObject, TKey>(u.Key, u.Current, newPosition, old));
            }
        }

        return new ChangeSet<TObject, TKey>(result);
    }

    public IChangeSet<TObject, TKey> ChangeComparer(KeyValueComparer<TObject, TKey> comparer)
    {
        _comparer = comparer;
        return ChangeSet<TObject, TKey>.Empty;
    }

    /// <summary>
    /// Initialises the specified changes.
    /// </summary>
    /// <param name="cache">The cache.</param>
    /// <returns>The change set.</returns>
    public IChangeSet<TObject, TKey> Load(ChangeAwareCache<TObject, TKey> cache)
    {
        // for the first batch of changes may have arrived before the comparer was set.
        // therefore infer the first batch of changes from the cache
        List = [.. cache.KeyValues.OrderBy(kv => kv, _comparer)];
        var initialItems = List.Select((t, index) => new Change<TObject, TKey>(ChangeReason.Add, t.Key, t.Value, index));
        return new ChangeSet<TObject, TKey>(initialItems);
    }

    public IChangeSet<TObject, TKey> Reorder()
    {
        var result = new List<Change<TObject, TKey>>();

        if (optimisations.HasFlag(SortOptimisations.IgnoreEvaluates))
        {
            // reorder entire sequence and do not calculate moves
            List = [.. List.OrderBy(kv => kv, _comparer)];
        }
        else
        {
            var index = -1;
            foreach (var item in List.OrderBy(t => t, _comparer).ToList())
            {
                var current = item;
                index++;

                // Cannot use binary search as Resort is implicit of a mutable change
                var existing = List[index];
                var areEqual = EqualityComparer<TKey>.Default.Equals(current.Key, existing.Key);
                if (areEqual)
                {
                    continue;
                }

                var old = List.IndexOf(current);
                List.RemoveAt(old);
                List.Insert(index, current);

                result.Add(new Change<TObject, TKey>(current.Key, current.Value, index, old));
            }
        }

        return new ChangeSet<TObject, TKey>(result);
    }

    /// <summary>
    /// Initialises the specified changes.
    /// </summary>
    /// <param name="cache">The cache.</param>
    public void Reset(ChangeAwareCache<TObject, TKey> cache) => List = [.. cache.KeyValues.OrderBy(kv => kv, _comparer)];

    private int GetCurrentPosition(KeyValuePair<TKey, TObject> item)
    {
        int index;

        if (optimisations.HasFlag(SortOptimisations.ComparesImmutableValuesOnly))
        {
            index = List.BinarySearch(item, _comparer);

            if (index < 0)
            {
                throw new SortException("Current position cannot be found.  Ensure the comparer includes a unique value, or do not specify ComparesImmutableValuesOnly");
            }
        }
        else
        {
            index = List.IndexOf(item);

            if (index < 0)
            {
                throw new SortException("Current position cannot be found. The item is not in the collection");
            }
        }

        return index;
    }

    private int GetInsertPositionBinary(KeyValuePair<TKey, TObject> item)
    {
        var index = List.BinarySearch(item, _comparer);

        if (index > 0)
        {
            var tempIndex = index;
            index = List.BinarySearch(tempIndex - 1, List.Count - tempIndex, item, _comparer);
            if (index > 0)
            {
                return tempIndex;
            }
        }

        return ~index;
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

        return List.Count;
    }
}
