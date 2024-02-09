// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class DynamicGrouper<TObject, TKey, TGroupKey>(Func<TObject, TKey, TGroupKey>? groupSelector = null) : IDisposable
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
        PerformAddOrUpdate(key, groupKey, item);

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
                    PerformAddOrUpdate(change.Key, _groupSelector(change.Current, change.Key), change.Current);
                    break;

                case ChangeReason.Update:
                    PerformUpdate(change.Key);
                    break;

                case ChangeReason.Refresh when _groupSelector is not null:
                    PerformRefresh(change.Key, _groupSelector(change.Current, change.Key), change.Current);
                    break;

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
        // Verify logic doesn't capture any non-empty groups
        Debug.Assert(_emptyGroups.All(static group => group.Cache.Count == 0), "Non empty Group in Empty Group HashSet");

        // Dispose/Remove any empty groups
        _emptyGroups
            .Where(static grp => grp.Count == 0)
            .ForEach(group =>
            {
                _groupCache.Remove(group.Key);
                group.Dispose();
            });
        _emptyGroups.Clear();

        // Make sure no empty ones were missed
        Debug.Assert(!_groupCache.Items.Any(static group => group.Cache.Count == 0), "Not all empty Groups were removed");

        // Emit any pending changes
        var changeSet = _groupCache.CaptureChanges();
        if (changeSet.Count != 0)
        {
            observer.OnNext(new GroupChangeSet<TObject, TKey, TGroupKey>(changeSet));
        }
    }

    public void Dispose() => _groupCache.Items.ForEach(group => (group as ManagedGroup<TObject, TKey, TGroupKey>)?.Dispose());

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

    private void PerformAddOrUpdate(TKey key, TGroupKey groupKey, TObject item)
    {
        // See if this item already has been grouped
        if (_groupKeys.TryGetValue(key, out var currentGroupKey))
        {
            // See if the key has changed
            if (EqualityComparer<TGroupKey>.Default.Equals(groupKey, currentGroupKey))
            {
                // GroupKey did not change, so just update the value in the group
                var optionalGroup = LookupGroup(currentGroupKey);
                if (optionalGroup.HasValue)
                {
                    optionalGroup.Value.Update(updater => updater.AddOrUpdate(item, key));
                    return;
                }

                Debug.Fail("If there is a GroupKey associated with a Key, the Group for that GroupKey should exist.");
            }
            else
            {
                // GroupKey changed, so remove from old and allow to be added below
                PerformRemove(key, currentGroupKey);
            }
        }

        // Find the right group and add the item
        PerformGroupAddOrUpdate(key, groupKey, item);
    }

    private void PerformGroupAddOrUpdate(TKey key, TGroupKey groupKey, TObject item)
    {
        var group = GetOrAddGroup(groupKey);
        group.Update(updater => updater.AddOrUpdate(item, key));
        _groupKeys[key] = groupKey;

        // Can't be empty since a value was just added
        _emptyGroups.Remove(group);
    }

    private void PerformRefresh(TKey key)
    {
        var optionalGroup = LookupGroup(key);
        if (optionalGroup.HasValue)
        {
            optionalGroup.Value.Update(updater => updater.Refresh(key));
        }
        else
        {
            Debug.Fail("Should not receive a refresh for an unknown Group Key");
        }
    }

    // When the GroupKey is available, check then and move the group if it changed
    private void PerformRefresh(TKey key, TGroupKey newGroupKey, TObject item)
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
                PerformGroupAddOrUpdate(key, newGroupKey, item);
            }
        }
        else
        {
            Debug.Fail("Should not receive a refresh for an unknown key");
        }
    }

    private void PerformRemove(TKey key)
    {
        if (_groupKeys.TryGetValue(key, out var groupKey))
        {
            PerformRemove(key, groupKey);
            _groupKeys.Remove(key);
        }
        else
        {
            Debug.Fail("Should not receive a Remove Event for an unknown key");
        }
    }

    private void PerformRemove(TKey key, TGroupKey groupKey)
    {
        var optionalGroup = LookupGroup(groupKey);
        if (optionalGroup.HasValue)
        {
            var currentGroup = optionalGroup.Value;
            currentGroup.Update(updater =>
            {
                updater.Remove(key);
                if (updater.Count == 0)
                {
                    _emptyGroups.Add(currentGroup);
                }
            });
        }
        else
        {
            Debug.Fail("Should not receive a Remove Event for an unknown Group Key");
        }
    }

    // Without the new group key, all that can be done is remove the old value
    // Consumer of the Grouper is resonsible for Adding the New Value.
    private void PerformUpdate(TKey key) => PerformRemove(key);
}
