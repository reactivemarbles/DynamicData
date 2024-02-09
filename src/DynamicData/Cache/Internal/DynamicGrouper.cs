// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class DynamicGrouper<TObject, TKey, TGroupKey>(Func<TObject, TKey, TGroupKey>? groupSelector = null)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    private readonly ChangeAwareCache<IGroup<TObject, TKey, TGroupKey>, TGroupKey> _groupCache = new();
    private readonly Dictionary<TKey, TGroupKey> _groupKeys = [];
    private readonly HashSet<ManagedGroup<TObject, TKey, TGroupKey>> _emptyGroups = [];
    private readonly Func<TObject, TKey, TGroupKey>? _groupSelector = groupSelector;

    public void AddOrUpdate(TKey key, TGroupKey groupKey, TObject item, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>>? observer = null)
    {
        PerformUpdate(key, groupKey, item);

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void ProcessChangeSet(IChangeSet<TObject, TKey> changeSet, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>>? observer = null)
    {
        foreach (var change in changeSet.ToConcreteType())
        {
            switch (change.Reason)
            {
                case ChangeReason.Add when _groupSelector is not null:
                    PerformAddOrUpdate(change.Key, _groupSelector(change.Current, change.Key), change.Current);
                    break;

                case ChangeReason.Remove:
                    PerformRemove(change.Key);
                    break;

                case ChangeReason.Update when _groupSelector is not null:
                    PerformUpdate(change.Key, _groupSelector(change.Current, change.Key), change.Current);
                    break;

                // Without the selector, all we can do is remove the old value
                case ChangeReason.Update:
                    PerformRemove(change.Key);
                    break;

                // With the selector, re-evalutate the GroupKey and move the group if it changed
                case ChangeReason.Refresh when _groupSelector is not null:
                    PerformRefresh(change.Key, _groupSelector(change.Current, change.Key), change.Current);
                    break;

                // Without the selector, just forward the refresh downstream
                case ChangeReason.Refresh:
                    PerformRefresh(change.Key);
                    break;
            }
        }

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void EmitChanges(IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        // Remove any empty groups
        _emptyGroups
            .Where(grp => grp.Count == 0)
            .ForEach(group => _groupCache.Remove(group.Key));
        _emptyGroups.Clear();

        // Emit any pending changes
        var changeSet = _groupCache.CaptureChanges();
        if (changeSet.Count != 0)
        {
            observer.OnNext(new GroupChangeSet<TObject, TKey, TGroupKey>(changeSet));
        }
    }

    private void PerformAddOrUpdate(TKey key, TGroupKey groupKey, TObject item)
    {
        var group = GetOrAddGroup(groupKey);
        group.Update(updater => updater.AddOrUpdate(item, key));
        _groupKeys[key] = groupKey;

        // Can't be empty since a value was just added
        _emptyGroups.Remove(group);
    }

    private void PerformUpdate(TKey key, TGroupKey newGroupKey, TObject current)
    {
        var groupKey = _groupKeys.Lookup(key);
        if (groupKey.HasValue)
        {
            var oldGroupKey = groupKey.Value;

            // See if the key has changed
            if (EqualityComparer<TGroupKey>.Default.Equals(newGroupKey, oldGroupKey))
            {
                // GroupKey did not change, so just update the value in the group
                var group = LookupGroup(oldGroupKey);
                if (group.HasValue)
                {
                    group.Value.Update(updater => updater.AddOrUpdate(current, key));
                }
                else
                {
                    PerformAddOrUpdate(key, newGroupKey, current);
                }
            }
            else
            {
                // GroupKey changed, so remove from old and add to new
                PerformRemove(key, oldGroupKey);
                PerformAddOrUpdate(key, newGroupKey, current);
            }
        }
        else
        {
            PerformAddOrUpdate(key, newGroupKey, current);
        }
    }

    private void PerformRemove(TKey key)
    {
        if (_groupKeys.TryGetValue(key, out var groupKey))
        {
            PerformRemove(key, groupKey);
            _groupKeys.Remove(key);
        }
    }

    private void PerformRemove(TKey key, TGroupKey groupKey)
    {
        var optionalGroup = LookupGroup(groupKey);
        if (optionalGroup.HasValue)
        {
            var currentGroup = optionalGroup.Value;
            currentGroup.Update(updater => updater.Remove(key));

            if (currentGroup.Count == 0)
            {
                _emptyGroups.Add(currentGroup);
            }
        }
    }

    private void PerformRefresh(TKey key, TGroupKey newGroupKey, TObject current)
    {
        if (_groupKeys.TryGetValue(key, out var groupKey))
        {
            // See if the key has changed
            if (EqualityComparer<TGroupKey>.Default.Equals(newGroupKey, groupKey))
            {
                // GroupKey did not change, so just refresh the value in the group
                PerformRefresh(key);
            }
            else
            {
                // GroupKey changed, so remove from old and add to new
                PerformRemove(key, groupKey);
                PerformAddOrUpdate(key, newGroupKey, current);
            }
        }
        else
        {
            PerformAddOrUpdate(key, newGroupKey, current);
        }
    }

    private void PerformRefresh(TKey key)
    {
        var optionalGroup = LookupGroup(key);
        if (optionalGroup.HasValue)
        {
            optionalGroup.Value.Update(updater => updater.Refresh(key));
        }
    }

    private Optional<ManagedGroup<TObject, TKey, TGroupKey>> LookupGroup(TKey key) =>
        _groupKeys.Lookup(key).Convert(LookupGroup);

    private Optional<ManagedGroup<TObject, TKey, TGroupKey>> LookupGroup(TGroupKey groupKey) =>
        _groupCache.Lookup(groupKey).Convert(static grp => (grp as ManagedGroup<TObject, TKey, TGroupKey>)!);

    private ManagedGroup<TObject, TKey, TGroupKey> GetOrAddGroup(TGroupKey groupKey) =>
        LookupGroup(groupKey).ValueOr(() =>
        {
            var newGroup = new ManagedGroup<TObject, TKey, TGroupKey>(groupKey);
            _groupCache.Add(newGroup, groupKey);
            return newGroup;
        });
}
