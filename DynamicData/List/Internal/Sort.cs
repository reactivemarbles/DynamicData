using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal sealed class Sort<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private  IComparer<T> _comparer;
        private readonly SortOptions _sortOptions;
        private readonly int _resetThreshold;
        private readonly IObservable<Unit> _resort;
        private readonly IObservable<IComparer<T>> _comparerObservable;


        public Sort([NotNull] IObservable<IChangeSet<T>> source, 
            [NotNull] IComparer<T> comparer, SortOptions sortOptions,
            IObservable<Unit> resort, 
            IObservable<IComparer<T>> comparerObservable,
            int resetThreshold)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            _source = source;
            _comparer = comparer;
            _sortOptions = sortOptions;
            _resetThreshold = resetThreshold;
            _resort = resort ?? Observable.Never<Unit>();
            _comparerObservable = comparerObservable ?? Observable.Never<IComparer<T>>();
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

                    return changes.TotalChanges > _resetThreshold ? Reset(orginal, target) : Process(target, changes);
                    
                });
                var resort = _resort.Synchronize(locker).Select(changes => Reorder(target));
                var changeComparer = _comparerObservable.Synchronize(locker).Select(comparer => ChangeComparer(target, comparer));

                return changed.Merge(resort).Merge(changeComparer).SubscribeSafe(observer);
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
                    case ListChangeReason.Replace:
                    {
                        var current = change.Item.Current;
                        //TODO: check whether an item should stay in the same position
                        //i.e. update and move
                        Remove(target, change.Item.Previous.Value);
                        Insert(target, current);
                        break;
                    }
                    case ListChangeReason.Remove:
                    {
                        var current = change.Item.Current;
                        Remove(target, current);
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
                    var old = target.IndexOf(item);
                    target.Move(old, index);
                }

            return target.CaptureChanges();
        }

        private IChangeSet<T> ChangeComparer(ChangeAwareList<T> target, IComparer<T> comparer)
        {
            _comparer = comparer;
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
            int index = _sortOptions == SortOptions.UseBinarySearch
                ? target.BinarySearch(item, _comparer)
                : target.IndexOf(item);

            if (index < 0)
                throw new SortException("Current item cannot be found");

            return index;
        }
    }
}
