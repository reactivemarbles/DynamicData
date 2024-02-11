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
    private Func<TObject, TKey, TGroupKey>? _groupSelector = groupSelector;

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

    // Re-evaluate the GroupSelector for each item and apply the changes so that each group only emits a single changset
    // Perform all the adds/removes for each group in a single step
    public void RegroupAll(IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        if (_groupSelector == null)
        {
            Debug.Fail("RegroupAll called without a GroupSelector. No changes will be made.");
            return;
        }

        // Create an array of tuples with data for items whose GroupKeys have changed
        var groupChanges = _groupCache.Items
            .Select(static group => group as ManagedGroup<TObject, TKey, TGroupKey>)
            .SelectMany(group => group!.Cache.KeyValues.Select(
                kvp => (KeyValuePair: kvp, OldGroup: group, NewGroupKey: _groupSelector(kvp.Value, kvp.Key))))
            .Where(static x => !EqualityComparer<TGroupKey>.Default.Equals(x.OldGroup.Key, x.NewGroupKey))
            .ToArray();

        // Build a list of the removals that need to happen (grouped by the old key)
        var pendingRemoves = groupChanges
            .GroupBy(
                static x => x.OldGroup.Key,
                static x => (x.KeyValuePair.Key, x.OldGroup))
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        // Build a list of the adds that need to happen (grouped by the new key)
        var pendingAddList = groupChanges
            .GroupBy(
                static x => x.NewGroupKey,
                static x => x.KeyValuePair)
            .ToList();

        // Iterate the list of groups that need something added (also maybe removed)
        foreach (var add in pendingAddList)
        {
            // Get a list of keys to be removed from this group (if any)
            var removeKeyList =
                pendingRemoves.TryGetValue(add.Key, out var removes)
                    ? removes.Select(static r => r.Key)
                    : Enumerable.Empty<TKey>();

            // Obtained the ManagedGroup instance and perform all of the pending updates at once
            var newGroup = GetOrAddGroup(add.Key);
            newGroup.Update(updater =>
            {
                updater.RemoveKeys(removeKeyList);
                updater.AddOrUpdate(add);
            });

            // Update the key cache
            foreach (var kvp in add)
            {
                _groupKeys[kvp.Key] = add.Key;
            }

            // Remove from the pendingRemove dictionary because these removes have been handled
            pendingRemoves.Remove(add.Key);
        }

        // Everything left in the Dictionary represents a group that had items removed but no items added
        foreach (var removeList in pendingRemoves.Values)
        {
            var group = removeList.First().OldGroup;
            group.Update(updater => updater.RemoveKeys(removeList.Select(static kvp => kvp.Key)));

            // If it is now empty, flag it for cleanup
            if (group.Count == 0)
            {
                _emptyGroups.Add(group);
            }
        }

        EmitChanges(observer);
    }

    public void SetGroupSelector(Func<TObject, TKey, TGroupKey> groupSelector, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        _groupSelector = groupSelector;
        RegroupAll(observer);
    }

    public void Initialize(IEnumerable<KeyValuePair<TKey, TObject>> initialValues, Func<TObject, TKey, TGroupKey> groupSelector, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        if (_groupSelector != null)
        {
            Debug.Fail("Initialize called when a GroupSelector is already present. No changes will be made.");
            return;
        }

        _groupSelector = groupSelector;
        foreach (var kvp in initialValues)
        {
            PerformAddOrUpdate(kvp.Key, _groupSelector(kvp.Value, kvp.Key), kvp.Value);
        }

        EmitChanges(observer);
    }

    public void EmitChanges(IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        // Verify logic doesn't capture any non-empty groups
        Debug.Assert(_emptyGroups.All(static group => group.Cache.Count == 0), "Non empty Group in Empty Group HashSet");

        // Dispose/Remove any empty groups
        foreach (var group in _emptyGroups)
        {
            if (group.Count == 0)
            {
                _groupCache.Remove(group.Key);
                group.Dispose();
            }
        }

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

    private static void PerformGroupRefresh(TKey key, in Optional<ManagedGroup<TObject, TKey, TGroupKey>> optionalGroup)
    {
        if (optionalGroup.HasValue)
        {
            optionalGroup.Value.Update(updater => updater.Refresh(key));
        }
        else
        {
            Debug.Fail("Should not receive a refresh for an unknown Group Key");
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

    private void PerformRefresh(TKey key) => PerformGroupRefresh(key, LookupGroup(key));

    // When the GroupKey is available, check then and move the group if it changed
    private void PerformRefresh(TKey key, TGroupKey newGroupKey, TObject item)
    {
        if (_groupKeys.TryGetValue(key, out var groupKey))
        {
            // See if the key has changed
            if (EqualityComparer<TGroupKey>.Default.Equals(newGroupKey, groupKey))
            {
                // GroupKey did not change, so just refresh the value in the group
                PerformGroupRefresh(key, LookupGroup(groupKey));
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
