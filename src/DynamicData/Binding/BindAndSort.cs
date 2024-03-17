// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

namespace DynamicData.Binding;

/*
 * A much more optimised bind where the sort forms part of the binding.
 *
 * This is much efficient as the prior sort mechanism would resort and clone the entire
 * collection upon every change in order that the sorted list could be transmitted to the bind operator.
 *
 */
internal sealed class BindAndSort<TObject, TKey>(
    IObservable<IChangeSet<TObject, TKey>> source,
    IComparer<TObject> comparer,
    BindAndSortOptions options,
    IList<TObject> target)
    where TObject : notnull
    where TKey : notnull
{
    private readonly Cache<TObject, TKey> _cache = new();
    private readonly object _locker = new();

    public IObservable<IChangeSet<TObject, TKey>> Run() =>
        source
            .Synchronize(_locker)
            // apply sorting as a side effect of the observable stream.
            .Do(changes =>
            {
                // clone to local cache so that we can sort entire set when threshold is over a certain size.
                _cache.Clone(changes);

                // apply sorted changes to the target collection
                if (target.Count == 0 || (options.ResetThreshold != 0 && options.ResetThreshold < changes.Count))
                {
                    Reset(_cache.Items.OrderBy(kv => kv, comparer));
                }
                else
                {
                    ApplyChanges(changes);
                }
            });

    private void Reset(IEnumerable<TObject> sorted)
    {
        if (target is ObservableCollectionExtended<TObject> observableCollectionExtended)
        {
            using (observableCollectionExtended.SuspendNotifications())
            {
                observableCollectionExtended.Load(sorted);
            }
        }
        else
        {
            target.Clear();
            foreach (var t in sorted)
            {
                target.Add(t);
            }
        }
    }

    private void ApplyChanges(IChangeSet<TObject, TKey> changes)
    {
        // iterate through collection, find sorted position and apply changes

        foreach (var change in changes.ToConcreteType())
        {
            var item = change.Current;

            switch (change.Reason)
            {
                case ChangeReason.Add:
                    {
                        var index = GetInsertPosition(item);
                        target.Insert(index, item);
                    }
                    break;
                case ChangeReason.Update:
                    {
                        var currentIndex = GetCurrentPosition(change.Previous.Value);
                        target.RemoveAt(currentIndex);

                        var index = GetInsertPosition(item);
                        target.Insert(index, item);
                    }
                    break;
                case ChangeReason.Remove:
                    {
                        var currentIndex = GetCurrentPosition(item);
                        target.RemoveAt(currentIndex);
                    }
                    break;
                case ChangeReason.Refresh:
                    {
                        var currentIndex = target.IndexOf(item);
                        var index = GetInsertPosition(item);

                        // this assumption may be dodgy as we may need to remove the original item first
                        if (index != currentIndex)
                        {
                            target.RemoveAt(currentIndex);
                            target.Insert(index, item);
                        }
                    }
                    break;
                case ChangeReason.Moved:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private int GetCurrentPosition(TObject item)
    {
        var index = options.UseBinarySearch ? target.BinarySearch(item, comparer) : target.IndexOf(item);

        if (index < 0)
        {
            throw new SortException($"Cannot find item: {typeof(TObject).Name} -> {item} from {target.Count} items");
        }

        return index;
    }

    private int GetInsertPosition(TObject item) => options.UseBinarySearch ? GetInsertPositionBinary(item) : GetInsertPositionLinear(item);

    private int GetInsertPositionBinary(TObject item)
    {
        var index = target.BinarySearch(item, comparer);
        var insertIndex = ~index;

        // sort is not returning uniqueness
        if (insertIndex < 0)
        {
            throw new SortException("Binary search has been specified, yet the sort does not yield uniqueness");
        }

        return insertIndex;
    }

    private int GetInsertPositionLinear(TObject item)
    {
        for (var i = 0; i < target.Count; i++)
        {
            if (comparer.Compare(item, target[i]) < 0)
            {
                return i;
            }
        }

        return target.Count;
    }
}
