// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class Sort<T>(IObservable<IChangeSet<T>> source, IComparer<T>? comparer, SortOptions sortOptions, IObservable<Unit>? resort, IObservable<IComparer<T>>? comparerObservable, int resetThreshold)
    where T : notnull
{
    private readonly IObservable<IComparer<T>> _comparerObservable = comparerObservable ?? Observable.Never<IComparer<T>>();
    private readonly IObservable<Unit> _resort = resort ?? Observable.Never<Unit>();
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    private IComparer<T> _comparer = comparer ?? Comparer<T>.Default;

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = new object();
                var original = new List<T>();
                var target = new ChangeAwareList<T>();

                var dataChanged = _source.Synchronize(locker).Select(
                    changes =>
                    {
                        if (resetThreshold > 1)
                        {
                            original.Clone(changes);
                        }

                        return changes.TotalChanges > resetThreshold ? Reset(original, target) : Process(target, changes);
                    });
                var resortSync = _resort.Synchronize(locker).Select(_ => Reorder(target));
                var changeComparer = _comparerObservable.Synchronize(locker).Select(comparer => ChangeComparer(target, comparer));

                return changeComparer.Merge(resortSync).Merge(dataChanged).Where(changes => changes.Count != 0).SubscribeSafe(observer);
            });

    private IChangeSet<T> ChangeComparer(ChangeAwareList<T> target, IComparer<T> comparer)
    {
        _comparer = comparer;
        if (resetThreshold > 0 && target.Count <= resetThreshold)
        {
            return Reorder(target);
        }

        var sorted = target.OrderBy(t => t, _comparer).ToList();
        target.Clear();
        target.AddRange(sorted);
        return target.CaptureChanges();
    }

    private int GetCurrentPosition(ChangeAwareList<T> target, T item)
    {
        var index = sortOptions == SortOptions.UseBinarySearch ? target.BinarySearch(item, _comparer) : target.IndexOf(item);

        if (index < 0)
        {
            throw new SortException($"Cannot find item: {typeof(T).Name} -> {item}");
        }

        return index;
    }

    private int GetInsertPosition(ChangeAwareList<T> target, T item) => sortOptions == SortOptions.UseBinarySearch ? GetInsertPositionBinary(target, item) : GetInsertPositionLinear(target, item);

    private int GetInsertPositionBinary(ChangeAwareList<T> target, T item)
    {
        var index = target.BinarySearch(item, _comparer);
        var insertIndex = ~index;

        // sort is not returning uniqueness
        if (insertIndex < 0)
        {
            throw new SortException("Binary search has been specified, yet the sort does not yield uniqueness");
        }

        return insertIndex;
    }

    private int GetInsertPositionLinear(ChangeAwareList<T> target, T item)
    {
        for (var i = 0; i < target.Count; i++)
        {
            if (_comparer.Compare(item, target[i]) < 0)
            {
                return i;
            }
        }

        return target.Count;
    }

    private void Insert(ChangeAwareList<T> target, T item)
    {
        var index = GetInsertPosition(target, item);
        target.Insert(index, item);
    }

    private IChangeSet<T> Process(ChangeAwareList<T> target, IChangeSet<T> changes)
    {
        // if all removes and not Clear, then more efficient to try clear range
        if (changes.TotalChanges == changes.Removes && changes.All(c => c.Reason != ListChangeReason.Clear) && changes.Removes > 1)
        {
            var removed = changes.Unified().Select(u => u.Current);
            target.RemoveMany(removed);
            return target.CaptureChanges();
        }

        return ProcessImpl(target, changes);
    }

    private IChangeSet<T> ProcessImpl(ChangeAwareList<T> target, IChangeSet<T> changes)
    {
        var refreshes = new List<T>(changes.Refreshes);

        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    {
                        var current = change.Item.Current;
                        Insert(target, current);
                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        var ordered = change.Range.OrderBy(t => t, _comparer).ToList();
                        if (target.Count == 0)
                        {
                            target.AddRange(ordered);
                        }
                        else
                        {
                            ordered.ForEach(item => Insert(target, item));
                        }

                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        var current = change.Item.Current;
                        Remove(target, current);
                        break;
                    }

                case ListChangeReason.Refresh:
                    {
                        // add to refresh list so position can be calculated
                        refreshes.Add(change.Item.Current);

                        // add to current list so downstream operators can receive a refresh
                        // notification, so get the latest index and pass the index up the chain
                        var indexed = target.IndexOfOptional(change.Item.Current).ValueOrThrow(() => new SortException($"Cannot find index of {typeof(T).Name} -> {change.Item.Current}. Expected to be in the list"));

                        target.Refresh(indexed.Item, indexed.Index);
                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var current = change.Item.Current;

                        // TODO: check whether an item should stay in the same position
                        // i.e. update and move
                        Remove(target, change.Item.Previous.Value);
                        Insert(target, current);
                        break;
                    }

                case ListChangeReason.RemoveRange:
                    {
                        target.RemoveMany(change.Range);
                        break;
                    }

                case ListChangeReason.Clear:
                    {
                        target.Clear();
                        break;
                    }
            }
        }

        // Now deal with refreshes [can be expensive]
        foreach (var item in refreshes)
        {
            var old = target.IndexOf(item);
            if (old == -1)
            {
                continue;
            }

            var newPosition = GetInsertPositionLinear(target, item);
            if (old < newPosition)
            {
                newPosition--;
            }

            if (old == newPosition)
            {
                continue;
            }

            target.Move(old, newPosition);
        }

        return target.CaptureChanges();
    }

    private void Remove(ChangeAwareList<T> target, T item)
    {
        var index = GetCurrentPosition(target, item);
        target.RemoveAt(index);
    }

    private IChangeSet<T> Reorder(ChangeAwareList<T> target)
    {
        var index = -1;
        foreach (var item in target.OrderBy(t => t, _comparer).ToList())
        {
            index++;

            var existing = target[index];

            // if item is in the same place,
            if (ReferenceEquals(item, existing))
            {
                continue;
            }

            // Cannot use binary search as Resort is implicit of a mutable change
            var old = target.IndexOf(item);
            target.Move(old, index);
        }

        return target.CaptureChanges();
    }

    private IChangeSet<T> Reset(List<T> original, ChangeAwareList<T> target)
    {
        var sorted = original.OrderBy(t => t, _comparer).ToList();
        target.Clear();
        target.AddRange(sorted);
        return target.CaptureChanges();
    }
}
