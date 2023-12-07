// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal static class FilteredIndexCalculator<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public static IList<Change<TObject, TKey>> Calculate(IKeyValueCollection<TObject, TKey> currentItems, IKeyValueCollection<TObject, TKey> previousItems, IChangeSet<TObject, TKey>? sourceUpdates)
    {
        if (currentItems.SortReason == SortReason.ComparerChanged || currentItems.SortReason == SortReason.InitialLoad)
        {
            // clear collection and rebuild
            var removed = previousItems.Select((item, index) => new Change<TObject, TKey>(ChangeReason.Remove, item.Key, item.Value, index));
            var newItems = currentItems.Select((item, index) => new Change<TObject, TKey>(ChangeReason.Add, item.Key, item.Value, index));

            return new List<Change<TObject, TKey>>(removed.Union(newItems));
        }

        var previousList = previousItems.ToList();
        var keyComparer = new KeyComparer<TObject, TKey>();

        var removes = previousItems.Except(currentItems, keyComparer).ToList();
        var adds = currentItems.Except(previousItems, keyComparer).ToList();
        var inBothKeys = new HashSet<TKey>(previousItems.Intersect(currentItems, keyComparer).Select(x => x.Key));

        var result = new List<Change<TObject, TKey>>();
        foreach (var remove in removes)
        {
            var index = previousList.IndexOf(remove);

            previousList.RemoveAt(index);
            result.Add(new Change<TObject, TKey>(ChangeReason.Remove, remove.Key, remove.Value, index));
        }

        foreach (var add in adds)
        {
            // find new insert position
            var index = previousList.BinarySearch(add, currentItems.Comparer);
            var insertIndex = ~index;
            previousList.Insert(insertIndex, add);
            result.Add(new Change<TObject, TKey>(ChangeReason.Add, add.Key, add.Value, insertIndex));
        }

        // Adds and removes have been accounted for
        // so check whether anything in the remaining change set have been moved ot updated
        var remainingItems = sourceUpdates.EmptyIfNull().Where(u => inBothKeys.Contains(u.Key) && (u.Reason == ChangeReason.Update || u.Reason == ChangeReason.Moved || u.Reason == ChangeReason.Refresh)).ToList();

        foreach (var change in remainingItems)
        {
            if (change.Reason == ChangeReason.Update)
            {
                var current = new KeyValuePair<TKey, TObject>(change.Key, change.Current);
                var previous = new KeyValuePair<TKey, TObject>(change.Key, change.Previous.Value);

                // remove from the actual index
                var removeIndex = previousList.IndexOf(previous);
                previousList.RemoveAt(removeIndex);

                // insert into the desired index
                var desiredIndex = previousList.BinarySearch(current, currentItems.Comparer);
                var insertIndex = ~desiredIndex;
                previousList.Insert(insertIndex, current);

                result.Add(new Change<TObject, TKey>(ChangeReason.Update, current.Key, current.Value, previous.Value, insertIndex, removeIndex));
            }
            else if (change.Reason == ChangeReason.Moved)
            {
                // TODO:  We have the index already, would be more efficient to calculate new position from the original index
                var current = new KeyValuePair<TKey, TObject>(change.Key, change.Current);

                var previousIndex = previousList.IndexOf(current);
                var desiredIndex = currentItems.IndexOf(current);

                if (previousIndex == desiredIndex)
                {
                    continue;
                }

                if (desiredIndex < 0)
                {
                    throw new SortException("Cannot determine current index");
                }

                previousList.RemoveAt(previousIndex);
                previousList.Insert(desiredIndex, current);
                result.Add(new Change<TObject, TKey>(current.Key, current.Value, desiredIndex, previousIndex));
            }
            else
            {
                // TODO: re-evaluate to check whether item should be moved
                result.Add(change);
            }
        }

        // Alternative to evaluate is to check order
        var evaluates = remainingItems.Where(c => c.Reason == ChangeReason.Refresh).OrderByDescending(x => new KeyValuePair<TKey, TObject>(x.Key, x.Current), currentItems.Comparer).ToList();

        // calculate moves.  Very expensive operation
        // TODO: Try and make this better
        foreach (var u in evaluates)
        {
            var current = new KeyValuePair<TKey, TObject>(u.Key, u.Current);
            var old = previousList.IndexOf(current);

            if (old == -1)
            {
                continue;
            }

            var newPosition = GetInsertPositionLinear(previousList, current, currentItems.Comparer);

            if (old < newPosition)
            {
                newPosition--;
            }

            if (old == newPosition)
            {
                continue;
            }

            previousList.RemoveAt(old);
            previousList.Insert(newPosition, current);
            result.Add(new Change<TObject, TKey>(u.Key, u.Current, newPosition, old));
        }

        return result;
    }

    private static int GetInsertPositionLinear(IList<KeyValuePair<TKey, TObject>> list, KeyValuePair<TKey, TObject> item, IComparer<KeyValuePair<TKey, TObject>> comparer)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (comparer.Compare(item, list[i]) < 0)
            {
                return i;
            }
        }

        return list.Count;
    }
}
