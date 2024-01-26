// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class ChangeSetMergeTracker<TObject, TKey>(Func<IEnumerable<ChangeSetCache<TObject, TKey>>> selectCaches, IComparer<TObject>? comparer, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
    where TKey : notnull
{
    private readonly ChangeAwareCache<TObject, TKey> _resultCache = new();
    private bool _hasCompleted;

    public void MarkComplete() => _hasCompleted = true;

    public void RemoveItems(IEnumerable<KeyValuePair<TKey, TObject>> items, IObserver<IChangeSet<TObject, TKey>>? observer = null)
    {
        var sourceCaches = selectCaches().ToArray();

        // Update the Published Value for each item being removed
        if (items is IList<KeyValuePair<TKey, TObject>> list)
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

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void RefreshItems(IEnumerable<TKey> keys, IObserver<IChangeSet<TObject, TKey>>? observer = null)
    {
        var sourceCaches = selectCaches().ToArray();

        // Update the Published Value for each item being removed
        if (keys is IList<TKey> list)
        {
            // zero allocation enumerator
            foreach (var key in EnumerableIList.Create(list))
            {
                ForceEvaluate(sourceCaches, key);
            }
        }
        else
        {
            foreach (var key in keys)
            {
                ForceEvaluate(sourceCaches, key);
            }
        }

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void ProcessChangeSet(IChangeSet<TObject, TKey> changes, IObserver<IChangeSet<TObject, TKey>>? observer = null)
    {
        var sourceCaches = selectCaches().ToArray();

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

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void EmitChanges(IObserver<IChangeSet<TObject, TKey>> observer)
    {
        var changeSet = _resultCache.CaptureChanges();
        if (changeSet.Count != 0)
        {
            observer.OnNext(changeSet);
        }

        if (_hasCompleted)
        {
            observer.OnCompleted();
        }
    }

    private void OnItemAdded(TObject item, TKey key)
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

    private void OnItemRemoved(ChangeSetCache<TObject, TKey>[] sourceCaches, TObject item, TKey key)
    {
        var cached = _resultCache.Lookup(key);

        // If this key has been observed and the current value is being removed
        if (cached.HasValue && CheckEquality(item, cached.Value))
        {
            // Perform a full update to select the new downstream value (or remove it)
            UpdateToBestValue(sourceCaches, key, cached);
        }
    }

    private void OnItemUpdated(ChangeSetCache<TObject, TKey>[] sources, TObject item, TKey key, in Optional<TObject> prev)
    {
        var cached = _resultCache.Lookup(key);

        // Received an update change for a key that hasn't been seen yet
        // So use the updated value
        if (!cached.HasValue)
        {
            _resultCache.Add(item, key);
            return;
        }

        // If the Previous value is missing or is the same as the current value
        var isUpdatingCurrent = !prev.HasValue || CheckEquality(prev.Value, cached.Value);

        if (comparer is null)
        {
            // If not using the comparer and the current value is being replaced by a different value
            if (isUpdatingCurrent && !CheckEquality(item, cached.Value))
            {
                // Update to the new value
                _resultCache.AddOrUpdate(item, key);
            }
        }
        else
        {
            // If using the comparer and the current value is one being updated
            if (isUpdatingCurrent)
            {
                // The known best value has been replaced, so pick a new one from all the choices
                UpdateToBestValue(sources, key, cached);
            }
            else
            {
                // If the current value isn't being replaced, its only required to check to see if the
                // new value is better than the current one
                if (ShouldReplace(item, cached.Value))
                {
                    _resultCache.AddOrUpdate(item, key);
                }
            }
        }
    }

    private void OnItemRefreshed(ChangeSetCache<TObject, TKey>[] sources, TObject item, TKey key)
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
        // AND the current value is the exact one being refreshed, then emit the refresh downstream
        if (((comparer is null) || !UpdateToBestValue(sources, key, cached)) && ReferenceEquals(cached.Value, item))
        {
            _resultCache.Refresh(key);
        }
    }

    private void ForceEvaluate(ChangeSetCache<TObject, TKey>[] sources, TKey key)
    {
        var cached = _resultCache.Lookup(key);

        // Received a refresh change for a key that hasn't been seen yet
        // Nothing can be done, so ignore it
        if (!cached.HasValue)
        {
            return;
        }

        UpdateToBestValue(sources, key, cached);
    }

    private bool UpdateToBestValue(ChangeSetCache<TObject, TKey>[] sources, TKey key, in Optional<TObject> current)
    {
        // Determine which value should be the one seen downstream
        var candidate = LookupBestValue(sources, key);
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

    private Optional<TObject> LookupBestValue(ChangeSetCache<TObject, TKey>[] sources, TKey key)
    {
        if (sources.Length == 0)
        {
            return Optional.None<TObject>();
        }

        var values = sources.Select(s => s.Cache.Lookup(key)).Where(opt => opt.HasValue);

        if (comparer is not null)
        {
            values = values.OrderBy(opt => opt.Value, comparer);
        }

        return values.FirstOrDefault();
    }

    private bool CheckEquality(TObject left, TObject right) =>
        ReferenceEquals(left, right) || (equalityComparer?.Equals(left, right) ?? false);

    // Return true if candidate should replace current as the observed downstream value
    private bool ShouldReplace(TObject candidate, TObject current) =>
        !ReferenceEquals(candidate, current) && (comparer?.Compare(candidate, current) < 0);
}
