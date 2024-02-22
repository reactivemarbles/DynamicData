// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
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
    private readonly SuspendTracker _suspendTracker = new();
    private Func<TObject, TKey, TGroupKey>? _groupSelector = groupSelector;

    public void AddOrUpdate(TKey key, TGroupKey groupKey, TObject item, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>>? observer = null)
    {
        // If not emitting the changes, then suspend the notifications
        // If changes will be emitted, then there is no need because it will generate at most one change per group
        PerformAddOrUpdate(key, groupKey, item, observer == null ? _suspendTracker : null);

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void ProcessChangeSet(IChangeSet<TObject, TKey> changeSet, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>>? observer = null)
    {
        var suspendTracker = (observer, changeSet.Count) switch
        {
            // If emitting the changeset and there is only one change, then there will be at most one change per group downstream
            // So there's no value in suspending the notifications
            (not null, 1) => null,

            // Otherwise, use the tracker so they get suspended
            _ => _suspendTracker,
        };

        foreach (var change in changeSet.ToConcreteType())
        {
            switch (change.Reason)
            {
                case ChangeReason.Add when _groupSelector is not null:
                    PerformAddOrUpdate(change.Key, _groupSelector(change.Current, change.Key), change.Current, suspendTracker);
                    break;

                case ChangeReason.Remove:
                    PerformRemove(change.Key, suspendTracker);
                    break;

                case ChangeReason.Update when _groupSelector is not null:
                    PerformAddOrUpdate(change.Key, _groupSelector(change.Current, change.Key), change.Current, suspendTracker);
                    break;

                case ChangeReason.Update:
                    PerformUpdate(change.Key, suspendTracker);
                    break;

                case ChangeReason.Refresh when _groupSelector is not null:
                    PerformRefresh(change.Key, _groupSelector(change.Current, change.Key), change.Current, suspendTracker);
                    break;

                case ChangeReason.Refresh:
                    PerformRefresh(change.Key, suspendTracker);
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

        // Create tuples with data for items whose GroupKeys have changed
        var groupChanges = _groupCache.Items
            .SelectMany(group => (group as ManagedGroup<TObject, TKey, TGroupKey>)!.Cache.KeyValues.Select(
                kvp => (KeyValuePair: kvp, OldGroup: (group as ManagedGroup<TObject, TKey, TGroupKey>)!, NewGroupKey: _groupSelector(kvp.Value, kvp.Key))))
            .Where(static x => !EqualityComparer<TGroupKey>.Default.Equals(x.OldGroup.Key, x.NewGroupKey))
            .ToArray();

        foreach (var change in groupChanges)
        {
            PerformGroupAddOrUpdate(change.KeyValuePair.Key, change.NewGroupKey, change.KeyValuePair.Value, _suspendTracker);
            _suspendTracker.Add(change.OldGroup);
            change.OldGroup.Update(updater =>
            {
                updater.Remove(change.KeyValuePair.Key);
                if (updater.Count == 0)
                {
                    _emptyGroups.Add(change.OldGroup);
                }
            });
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

        // No need for Suspend Tracker.  There can't be any subscribers to the Group Caches yet so they won't be emitting any changesets.
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

        _suspendTracker.Reset();

        // Emit any pending changes
        var changeSet = _groupCache.CaptureChanges();
        if (changeSet.Count != 0)
        {
            observer.OnNext(new GroupChangeSet<TObject, TKey, TGroupKey>(changeSet));
        }
    }

    public void Dispose()
    {
        _suspendTracker.Dispose();
        _groupCache.Items.ForEach(group => (group as ManagedGroup<TObject, TKey, TGroupKey>)?.Dispose());
    }

    private static void PerformGroupRefresh(TKey key, in Optional<ManagedGroup<TObject, TKey, TGroupKey>> optionalGroup, SuspendTracker? suspendTracker = null)
    {
        if (optionalGroup.HasValue)
        {
            suspendTracker?.Add(optionalGroup.Value);
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

    private void PerformAddOrUpdate(TKey key, TGroupKey groupKey, TObject item, SuspendTracker? suspendTracker = null)
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
                    suspendTracker?.Add(optionalGroup.Value);
                    optionalGroup.Value.Update(updater => updater.AddOrUpdate(item, key));
                    return;
                }

                Debug.Fail("If there is a GroupKey associated with a Key, the Group for that GroupKey should exist.");
            }
            else
            {
                // GroupKey changed, so remove from old and allow to be added below
                PerformRemove(key, currentGroupKey, suspendTracker);
            }
        }

        // Find the right group and add the item
        PerformGroupAddOrUpdate(key, groupKey, item, suspendTracker);
    }

    private void PerformGroupAddOrUpdate(TKey key, TGroupKey groupKey, TObject item, SuspendTracker? suspendTracker = null)
    {
        var group = GetOrAddGroup(groupKey);
        suspendTracker?.Add(group);
        group.Update(updater => updater.AddOrUpdate(item, key));
        _groupKeys[key] = groupKey;

        // Can't be empty since a value was just added
        _emptyGroups.Remove(group);
    }

    private void PerformRefresh(TKey key, SuspendTracker? suspendTracker = null) => PerformGroupRefresh(key, LookupGroup(key), suspendTracker);

    // When the GroupKey is available, check then and move the group if it changed
    private void PerformRefresh(TKey key, TGroupKey newGroupKey, TObject item, SuspendTracker? suspendTracker = null)
    {
        if (_groupKeys.TryGetValue(key, out var groupKey))
        {
            // See if the key has changed
            if (EqualityComparer<TGroupKey>.Default.Equals(newGroupKey, groupKey))
            {
                // GroupKey did not change, so just refresh the value in the group
                PerformGroupRefresh(key, LookupGroup(groupKey), suspendTracker);
            }
            else
            {
                // GroupKey changed, so remove from old and add to new
                PerformRemove(key, groupKey, suspendTracker);
                PerformGroupAddOrUpdate(key, newGroupKey, item, suspendTracker);
            }
        }
        else
        {
            Debug.Fail("Should not receive a refresh for an unknown key");
        }
    }

    private void PerformRemove(TKey key, SuspendTracker? suspendTracker = null)
    {
        if (_groupKeys.TryGetValue(key, out var groupKey))
        {
            PerformRemove(key, groupKey, suspendTracker);
            _groupKeys.Remove(key);
        }
        else
        {
            Debug.Fail("Should not receive a Remove Event for an unknown key");
        }
    }

    private void PerformRemove(TKey key, TGroupKey groupKey, SuspendTracker? suspendTracker = null)
    {
        var optionalGroup = LookupGroup(groupKey);
        if (optionalGroup.HasValue)
        {
            var currentGroup = optionalGroup.Value;
            suspendTracker?.Add(currentGroup);
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
    private void PerformUpdate(TKey key, SuspendTracker? suspendTracker = null) => PerformRemove(key, suspendTracker);

    private sealed class SuspendTracker : IDisposable
    {
        private readonly HashSet<TGroupKey> _trackedKeys = [];
        private CompositeDisposable _disposables = [];

        public bool HasItems => _disposables.Count > 0;

        public void Add(ManagedGroup<TObject, TKey, TGroupKey> managedGroup)
        {
            if (_trackedKeys.Add(managedGroup.Key))
            {
                _disposables.Add(managedGroup.SuspendNotifications());
            }
        }

        public void Reset()
        {
            if (_disposables.Count > 0)
            {
                _disposables.Dispose();
                _disposables = [];
                _trackedKeys.Clear();
            }
        }

        public void Dispose() => _disposables.Dispose();
    }
}
