// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal class MergedCacheChangeTracker<TDestination, TDestinationKey>
    where TDestination : notnull
    where TDestinationKey : notnull
{
    private readonly ChangeAwareCache<TDestination, TDestinationKey> _resultCache;
    private readonly Func<MergeContainer[]> _selectContainers;
    private readonly IComparer<TDestination>? _comparer;
    private readonly IEqualityComparer<TDestination>? _equalityComparer;

    public MergedCacheChangeTracker(Func<MergeContainer[]> selectContainers, IComparer<TDestination>? comparer, IEqualityComparer<TDestination>? equalityComparer)
    {
        _resultCache = new ChangeAwareCache<TDestination, TDestinationKey>();
        _selectContainers = selectContainers;
        _comparer = comparer;
        _equalityComparer = equalityComparer;
    }

    public void RemoveItems(IEnumerable<KeyValuePair<TDestinationKey, TDestination>> items, IObserver<IChangeSet<TDestination, TDestinationKey>> observer)
    {
        var sourceCaches = _selectContainers();

        // Update the Published Value for each item being removed
        if (items is IList<KeyValuePair<TDestinationKey, TDestination>> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                OnItemRemoved(sourceCaches, item.Value, item.Key);
            }
        }
        else
        {
            foreach (var item in items)
            {
                OnItemRemoved(sourceCaches, item.Value, item.Key);
            }
        }

        EmitChanges(observer);
    }

    public void ProcessChangeSet(IChangeSet<TDestination, TDestinationKey> changes, IObserver<IChangeSet<TDestination, TDestinationKey>> observer)
    {
        var sourceCaches = _selectContainers();

        foreach (var change in changes.ToConcreteType())
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    OnItemAdded(change.Current, change.Key);
                    break;

                case ChangeReason.Remove:
                    OnItemRemoved(sourceCaches, change.Current, change.Key);
                    break;

                case ChangeReason.Update:
                    OnItemUpdated(sourceCaches, change.Current, change.Key, change.Previous);
                    break;

                case ChangeReason.Refresh:
                    OnItemRefreshed(sourceCaches, change.Current, change.Key);
                    break;
            }
        }

        EmitChanges(observer);
    }

    private void EmitChanges(IObserver<IChangeSet<TDestination, TDestinationKey>> observer)
    {
        var changeSet = _resultCache.CaptureChanges();
        if (changeSet.Count != 0)
        {
            observer.OnNext(changeSet);
        }
    }

    private void OnItemAdded(TDestination item, TDestinationKey key)
    {
        var cached = _resultCache.Lookup(key);

        // If no current value, then add it
        if (!cached.HasValue)
        {
            _resultCache.Add(item, key);
        }
        else if (ShouldReplace(item, cached.Value))
        {
            _resultCache.AddOrUpdate(item, key);
        }
    }

    private void OnItemRemoved(MergeContainer[] sourceCaches, TDestination item, TDestinationKey key)
    {
        var cached = _resultCache.Lookup(key);

        // If this key has been observed and the current value is being removed
        if (cached.HasValue && CheckEquality(item, cached.Value))
        {
            // Perform a full update to select the new downstream value (or remove it)
            UpdateToBestValue(sourceCaches, key, cached);
        }
    }

    private void OnItemUpdated(MergeContainer[] sources, TDestination item, TDestinationKey key, Optional<TDestination> prev)
    {
        var cached = _resultCache.Lookup(key);

        // Received an update change for a key that hasn't been seen yet
        // So use the updated value
        if (!cached.HasValue)
        {
            _resultCache.Add(item, key);
            return;
        }

        if (_comparer is null)
        {
            // If the current value (or there is no way to tell) is being replaced by a different value
            if ((!prev.HasValue || CheckEquality(prev.Value, cached.Value)) && !CheckEquality(item, cached.Value))
            {
                // Update to the new value
                _resultCache.AddOrUpdate(item, key);
            }
        }
        else
        {
            // The current value is being replaced (or there is no way to tell), so do a full update to select the best one from all the choices
            if (!prev.HasValue || CheckEquality(prev.Value, cached.Value))
            {
                UpdateToBestValue(sources, key, cached);
            }
            else
            {
                // If the current value isn't being replaced, check to see if the replacement value is better than the current one
                if (ShouldReplace(item, cached.Value))
                {
                    _resultCache.AddOrUpdate(item, key);
                }
            }
        }
    }

    private void OnItemRefreshed(MergeContainer[] sources, TDestination item, TDestinationKey key)
    {
        var cached = _resultCache.Lookup(key);

        // Received a refresh change for a key that hasn't been seen yet
        // Nothing can be done, so ignore it
        if (!cached.HasValue)
        {
            return;
        }

        // In the sorting case, a refresh requires doing a full update because any change could alter what the best value is
        // If we don't care about sorting OR if we do care, but re-selecting the best value didn't change anything
        // AND the current value is the one being refreshed
        if (((_comparer is null) || !UpdateToBestValue(sources, key, cached)) && CheckEquality(cached.Value, item))
        {
            // Emit the refresh downstream
            _resultCache.Refresh(key);
        }
    }

    private bool UpdateToBestValue(MergeContainer[] sources, TDestinationKey key, Optional<TDestination> current)
    {
        // Determine which value should be the one seen downstream
        var candidate = SelectValue(sources, key);
        if (candidate.HasValue)
        {
            // If there isn't a current value
            if (!current.HasValue)
            {
                _resultCache.Add(candidate.Value, key);
                return true;
            }

            // If the candidate value isn't the same as the current value
            if (!CheckEquality(current.Value, candidate.Value))
            {
                _resultCache.AddOrUpdate(candidate.Value, key);
                return true;
            }

            // The value seen downstream is the one that should be
            return false;
        }

        // No best candidate available
        _resultCache.Remove(key);
        return true;
    }

    private Optional<TDestination> SelectValue(MergeContainer[] sources, TDestinationKey key)
    {
        if (sources.Length == 0)
        {
            return Optional.None<TDestination>();
        }

        var values = sources.Select(s => s.Cache.Lookup(key)).Where(opt => opt.HasValue);

        if (_comparer is not null)
        {
            values = values.OrderBy(opt => opt.Value, _comparer);
        }

        return values.FirstOrDefault();
    }

    private bool CheckEquality(TDestination left, TDestination right) =>
        ReferenceEquals(left, right) || (_equalityComparer?.Equals(left, right) ?? (_comparer?.Compare(left, right) == 0));

    // Return true if candidate should replace current as the observed downstream value
    private bool ShouldReplace(TDestination candidate, TDestination current) =>
        !ReferenceEquals(candidate, current) && (_comparer?.Compare(candidate, current) < 0);

    internal class MergeContainer
    {
        public MergeContainer(IObservable<IChangeSet<TDestination, TDestinationKey>> source)
        {
            Source = source.IgnoreSameReferenceUpdate().Do(Clone);
        }

        public Cache<TDestination, TDestinationKey> Cache { get; } = new();

        public IObservable<IChangeSet<TDestination, TDestinationKey>> Source { get; }

        private void Clone(IChangeSet<TDestination, TDestinationKey> changes) => Cache.Clone(changes);
    }
}
