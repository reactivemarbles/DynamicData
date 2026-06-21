// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the DynamicGrouper class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="groupSelector">The groupSelector value.</param>
internal sealed class DynamicGrouper<TObject, TKey, TGroupKey>(Func<TObject, TKey, TGroupKey>? groupSelector = null) : IDisposable
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The _groupCache field.
    /// </summary>
    private readonly ChangeAwareCache<IGroup<TObject, TKey, TGroupKey>, TGroupKey> _groupCache = new();

    /// <summary>
    /// The _groupKeys field.
    /// </summary>
    private readonly Dictionary<TKey, TGroupKey> _groupKeys = [];

    /// <summary>
    /// The _emptyGroups field.
    /// </summary>
    private readonly HashSet<ManagedGroup<TObject, TKey, TGroupKey>> _emptyGroups = [];

    /// <summary>
    /// The _suspendTracker field.
    /// </summary>
    private readonly SuspendTracker _suspendTracker = new();

    /// <summary>
    /// The _groupSelector field.
    /// </summary>
    private Func<TObject, TKey, TGroupKey>? _groupSelector = groupSelector;

    /// <summary>
    /// Executes the AddOrUpdate operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="groupKey">The groupKey value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the ProcessChangeSet operation.
    /// </summary>
    /// <param name="changeSet">The changeSet value.</param>
    /// <param name="observer">The observer value.</param>
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
            ProcessChange(change, suspendTracker);
        }

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    /// <summary>
    /// Executes the ProcessChange operation.
    /// </summary>
    /// <param name="change">The change value.</param>
    public void ProcessChange(Change<TObject, TKey> change) => ProcessChange(change, _suspendTracker);

    /// <summary>
    /// Executes the ProcessChange operation.
    /// </summary>
    /// <param name="change">The change value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
    private void ProcessChange(Change<TObject, TKey> change, SuspendTracker? suspendTracker)
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
    // Re-evaluate the GroupSelector for each item and apply the changes so that each group only emits a single changset
    // Perform all the adds/removes for each group in a single step

    /// <summary>
    /// Executes the RegroupAll operation.
    /// </summary>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the SetGroupSelector operation.
    /// </summary>
    /// <param name="groupSelector">The groupSelector value.</param>
    /// <param name="observer">The observer value.</param>
    public void SetGroupSelector(Func<TObject, TKey, TGroupKey> groupSelector, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        _groupSelector = groupSelector;
        RegroupAll(observer);
    }

    /// <summary>
    /// Executes the Initialize operation.
    /// </summary>
    /// <param name="initialValues">The initialValues value.</param>
    /// <param name="groupSelector">The groupSelector value.</param>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the EmitChanges operation.
    /// </summary>
    /// <param name="observer">The observer value.</param>
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

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose()
    {
        _suspendTracker.Dispose();
        _groupCache.Items.ForEach(group => (group as ManagedGroup<TObject, TKey, TGroupKey>)?.Dispose());
    }

    /// <summary>
    /// Executes the PerformGroupRefresh operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="optionalGroup">The optionalGroup value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
    private static void PerformGroupRefresh(TKey key, in ReactiveUI.Primitives.Optional<ManagedGroup<TObject, TKey, TGroupKey>> optionalGroup, SuspendTracker? suspendTracker = null)
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

    /// <summary>
    /// Executes the LookupGroup operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    private ReactiveUI.Primitives.Optional<ManagedGroup<TObject, TKey, TGroupKey>> LookupGroup(TKey key) =>
        _groupKeys.Lookup(key).Convert(LookupGroup);

    /// <summary>
    /// Executes the LookupGroup operation.
    /// </summary>
    /// <param name="groupKey">The groupKey value.</param>
    /// <returns>The result of the operation.</returns>
    private ReactiveUI.Primitives.Optional<ManagedGroup<TObject, TKey, TGroupKey>> LookupGroup(TGroupKey groupKey) =>
        _groupCache.Lookup(groupKey).Convert(static grp => (grp as ManagedGroup<TObject, TKey, TGroupKey>)!);

    /// <summary>
    /// Executes the GetOrAddGroup operation.
    /// </summary>
    /// <param name="groupKey">The groupKey value.</param>
    /// <returns>The result of the operation.</returns>
    private ManagedGroup<TObject, TKey, TGroupKey> GetOrAddGroup(TGroupKey groupKey) =>
        LookupGroup(groupKey).ValueOr(() =>
        {
            var newGroup = new ManagedGroup<TObject, TKey, TGroupKey>(groupKey);
            _groupCache.Add(newGroup, groupKey);
            return newGroup;
        });

    /// <summary>
    /// Executes the PerformAddOrUpdate operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="groupKey">The groupKey value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
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

    /// <summary>
    /// Executes the PerformGroupAddOrUpdate operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="groupKey">The groupKey value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
    private void PerformGroupAddOrUpdate(TKey key, TGroupKey groupKey, TObject item, SuspendTracker? suspendTracker = null)
    {
        var group = GetOrAddGroup(groupKey);
        suspendTracker?.Add(group);
        group.Update(updater => updater.AddOrUpdate(item, key));
        _groupKeys[key] = groupKey;

        // Can't be empty since a value was just added
        _emptyGroups.Remove(group);
    }

    /// <summary>
    /// Executes the PerformRefresh operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
    private void PerformRefresh(TKey key, SuspendTracker? suspendTracker = null) => PerformGroupRefresh(key, LookupGroup(key), suspendTracker);
    // When the GroupKey is available, check then and move the group if it changed

    /// <summary>
    /// Executes the PerformRefresh operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="newGroupKey">The newGroupKey value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
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

    /// <summary>
    /// Executes the PerformRemove operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
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

    /// <summary>
    /// Executes the PerformRemove operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="groupKey">The groupKey value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
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

    /// <summary>
    /// Executes the PerformUpdate operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="suspendTracker">The suspendTracker value.</param>
    private void PerformUpdate(TKey key, SuspendTracker? suspendTracker = null) => PerformRemove(key, suspendTracker);

/// <summary>
/// Provides members for the SuspendTracker class.
/// </summary>
private sealed class SuspendTracker : IDisposable
    {
        /// <summary>
        /// The _trackedKeys field.
        /// </summary>
        private readonly HashSet<TGroupKey> _trackedKeys = [];

        /// <summary>
        /// The _disposables field.
        /// </summary>
        private CompositeDisposable _disposables = [];

        /// <summary>
        /// Gets the HasItems value.
        /// </summary>
        public bool HasItems => _disposables.Count > 0;

        /// <summary>
        /// Executes the Add operation.
        /// </summary>
        /// <param name="managedGroup">The managedGroup value.</param>
        public void Add(ManagedGroup<TObject, TKey, TGroupKey> managedGroup)
        {
            if (_trackedKeys.Add(managedGroup.Key))
            {
                _disposables.Add(managedGroup.SuspendNotifications());
            }
        }

        /// <summary>
        /// Executes the Reset operation.
        /// </summary>
        public void Reset()
        {
            if (_disposables.Count > 0)
            {
                _disposables.Dispose();
                _disposables = [];
                _trackedKeys.Clear();
            }
        }

        /// <summary>
        /// Executes the Dispose operation.
        /// </summary>
        public void Dispose() => _disposables.Dispose();
    }
}
