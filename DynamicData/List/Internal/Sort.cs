using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class Sort<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly SortOptions _sortOptions;
        private readonly int _resetThreshold;
        private readonly IObservable<Unit> _resort;
        private readonly IObservable<IComparer<T>> _comparerObservable;
        private readonly IEqualityComparer<T> _referencEqualityComparer = ReferenceEqualityComparer<T>.Instance;
        private IComparer<T> _comparer;

        public Sort([NotNull] IObservable<IChangeSet<T>> source,
            [NotNull] IComparer<T> comparer, SortOptions sortOptions,
            IObservable<Unit> resort,
            IObservable<IComparer<T>> comparerObservable,
            int resetThreshold)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _resort = resort ?? Observable.Never<Unit>();
            _comparerObservable = comparerObservable ?? Observable.Never<IComparer<T>>();
            _comparer = comparer;
            _sortOptions = sortOptions;
            _resetThreshold = resetThreshold;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var locker = new object();
                var orginal = new ChangeAwareList<T>();
                var target = new ChangeAwareList<T>();

                var changed = _source.Synchronize(locker).Select(changes =>
                {
                    if (_resetThreshold > 1)
                        orginal.Clone(changes);

                    return changes.TotalChanges > _resetThreshold && _comparer!=null ? Reset(orginal, target) : Process(target, changes);
                });
                var resort = _resort.Synchronize(locker).Select(changes => Reorder(target));
                var changeComparer = _comparerObservable.Synchronize(locker).Select(comparer => ChangeComparer(target, comparer));

                return changed.Merge(resort).Merge(changeComparer)
                    .Where(changes => changes.Count != 0)
                    .SubscribeSafe(observer);
            });
        }

        private IChangeSet<T> Process(ChangeAwareList<T> target, IChangeSet<T> changes)
        {
            //if all removes and not Clear, then more efficient to try clear range
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
            if (_comparer == null)
            {
                target.Clone(changes);
                return target.CaptureChanges();
            }

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
                        //add to refresh list so position can be calculated
                        refreshes.Add(change.Item.Current);

                        //add to current list so downstream operators can receive a refresh
                        //notification, so get the latest index and pass the index up the chain
                        var indexed = target
                            .IndexOfOptional(change.Item.Current, ReferenceEqualityComparer<T>.Instance)
                            .ValueOrThrow(() => new SortException($"Cannot find index of {typeof(T).Name} -> {change.Item.Current}. Expected to be in the list"));

                        target.Refresh(indexed.Item, indexed.Index);
                        break;
                    }
                    case ListChangeReason.Replace:
                        {
                            var current = change.Item.Current;
                            //TODO: check whether an item should stay in the same position
                            //i.e. update and move
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

            //Now deal with refreshes [can be expensive]
            foreach (var item in refreshes)
            {
                var old = target.IndexOf(item);
                if (old == -1) continue;

                int newposition = GetInsertPositionLinear(target, item);
                if (old < newposition)
                    newposition--; 

                if (old == newposition)
                    continue;

                target.Move(old, newposition);
            }
            return target.CaptureChanges();
        }

        private IChangeSet<T> Reorder(ChangeAwareList<T> target)
        {
            int index = -1;
            var sorted = target.OrderBy(t => t, _comparer).ToList();
            foreach (var item in sorted)
            {
                index++;

                var existing = target[index];
                //if item is in the same place, 
                if (ReferenceEquals(item, existing)) continue;

                //Cannot use binary search as Resort is implicit of a mutable change
                var old = target.IndexOf(item, _referencEqualityComparer);
                target.Move(old, index);
            }

            return target.CaptureChanges();
        }

        private IChangeSet<T> ChangeComparer(ChangeAwareList<T> target, IComparer<T> comparer)
        {
            _comparer = comparer;
            if (_resetThreshold > 0 && target.Count <= _resetThreshold)
                return  Reorder(target);

            var sorted = target.OrderBy(t => t, _comparer).ToList();
            target.Clear();
            target.AddRange(sorted);
            return target.CaptureChanges();
        }

        private IChangeSet<T> Reset(ChangeAwareList<T> original, ChangeAwareList<T> target)
        {
            var sorted = original.OrderBy(t => t, _comparer).ToList();
            target.Clear();
            target.AddRange(sorted);
            return target.CaptureChanges();
        }

        private void Remove(ChangeAwareList<T> target, T item)
        {
            var index = GetCurrentPosition(target, item);
            target.RemoveAt(index);
        }

        private void Insert(ChangeAwareList<T> target, T item)
        {
            var index = GetInsertPosition(target, item);
            target.Insert(index, item);
        }

        private int GetInsertPosition(ChangeAwareList<T> target, T item)
        {
            return _sortOptions == SortOptions.UseBinarySearch
                ? GetInsertPositionBinary(target, item)
                : GetInsertPositionLinear(target, item);
        }

        private int GetInsertPositionLinear(ChangeAwareList<T> target, T item)
        {
            for (var i = 0; i < target.Count; i++)
            {
                if (_comparer.Compare(item, target[i]) < 0)
                    return i;
            }
            return target.Count;
        }

        private int GetInsertPositionBinary(ChangeAwareList<T> target, T item)
        {
            int index = target.BinarySearch(item, _comparer);
            int insertIndex = ~index;

            //sort is not returning uniqueness
            if (insertIndex < 0)
                throw new SortException("Binary search has been specified, yet the sort does not yeild uniqueness");
            return insertIndex;
        }

        private int GetCurrentPosition(ChangeAwareList<T> target, T item)
        {
            int index;
            index = _sortOptions == SortOptions.UseBinarySearch 
                ? target.BinarySearch(item, _comparer) 
                : target.IndexOf(item, _referencEqualityComparer);

            if (index < 0)
                throw new SortException($"Cannot find item: {typeof(T).Name} -> {item}");

            return index;
        }
    }
}
