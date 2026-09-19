// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ChangeSetMergeTracker class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="selectCaches">The selectCaches value.</param>
/// <param name="comparer">The comparer value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
internal sealed class ChangeSetMergeTracker<TObject, TKey>(Func<IEnumerable<ChangeSetCache<TObject, TKey>>> selectCaches, IComparer<TObject>? comparer, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _resultCache field.
    /// </summary>
    private readonly ChangeAwareCache<TObject, TKey> _resultCache = new();

    /// <summary>
    /// The _equalityComparer field.
    /// </summary>
    private readonly IEqualityComparer<TObject> _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;

    /// <summary>
    /// The _hasCompleted field.
    /// </summary>
    private bool _hasCompleted;

    /// <summary>
    /// Executes the MarkComplete operation.
    /// </summary>
    public void MarkComplete() => _hasCompleted = true;

    /// <summary>
    /// Executes the RemoveItems operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the RefreshItems operation.
    /// </summary>
    /// <param name="keys">The keys value.</param>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the ProcessChangeSet operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the EmitChanges operation.
    /// </summary>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the OnItemAdded operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <param name="key">The key value.</param>
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

    /// <summary>
    /// Executes the OnItemRemoved operation.
    /// </summary>
    /// <param name="sourceCaches">The sourceCaches value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="key">The key value.</param>
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

    /// <summary>
    /// Executes the OnItemUpdated operation.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="key">The key value.</param>
    /// <param name="prev">The prev value.</param>
    private void OnItemUpdated(ChangeSetCache<TObject, TKey>[] sources, TObject item, TKey key, in ReactiveUI.Primitives.Optional<TObject> prev)
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

    /// <summary>
    /// Executes the OnItemRefreshed operation.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="key">The key value.</param>
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
        if (((comparer is null) || !UpdateToBestValue(sources, key, cached)) && CheckEquality(cached.Value, item))
        {
            _resultCache.Refresh(key);
        }
    }

    /// <summary>
    /// Executes the ForceEvaluate operation.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="key">The key value.</param>
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

    /// <summary>
    /// Executes the UpdateToBestValue operation.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="key">The key value.</param>
    /// <param name="current">The current value.</param>
    /// <returns>The result of the operation.</returns>
    private bool UpdateToBestValue(ChangeSetCache<TObject, TKey>[] sources, TKey key, in ReactiveUI.Primitives.Optional<TObject> current)
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

    /// <summary>
    /// Executes the LookupBestValue operation.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    private ReactiveUI.Primitives.Optional<TObject> LookupBestValue(ChangeSetCache<TObject, TKey>[] sources, TKey key)
    {
        if (sources.Length == 0)
        {
            return ReactiveUI.Primitives.Optional<TObject>.None;
        }

        var values = sources.Select(s => s.Cache.Lookup(key)).Where(opt => opt.HasValue);

        if (comparer is not null)
        {
            values = values.OrderBy(opt => opt.Value, comparer);
        }

        return values.FirstOrDefault();
    }

    /// <summary>
    /// Executes the CheckEquality operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    private bool CheckEquality(TObject left, TObject right) =>
        _equalityComparer.Equals(left, right);
    // Return true if candidate should replace current as the observed downstream value

    /// <summary>
    /// Executes the ShouldReplace operation.
    /// </summary>
    /// <param name="candidate">The candidate value.</param>
    /// <param name="current">The current value.</param>
    /// <returns>The result of the operation.</returns>
    private bool ShouldReplace(TObject candidate, TObject current) =>
        comparer?.Compare(candidate, current) < 0;
}
