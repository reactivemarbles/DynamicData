// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class DynamicGrouper<TObject, TKey, TGroupKey>
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    private readonly ChangeAwareCache<ManagedGroup<TObject, TKey, TGroupKey>, TGroupKey> _groupCache = new();
    private readonly Dictionary<TKey, TGroupKey> _groupKeys = [];

    public void AddOrUpdate(TObject item, TKey key, TGroupKey groupKey, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>>? observer = null)
    {
        if (_groupKeys.TryGetValue(key, out var currentGroupKey))
        {
            if (!EqualityComparer<TGroupKey>.Default.Equals(groupKey, currentGroupKey))
            {
                var currentGroup = _groupCache.Lookup(currentGroupKey).Value;
                currentGroup.Update(updater => updater.Remove(key));
                if (currentGroup.Count == 0)
                {
                    _groupCache.Remove(currentGroupKey);
                }
            }
        }

        var group = GetOrAddGroup(groupKey);
        group.Update(updater => updater.AddOrUpdate(item, key));
        _groupKeys[key] = groupKey;

        if (observer != null)
        {
            EmitChanges(observer);
        }
    }

    public void ProcessChanges(IChangeSet<TObject, TKey> changeSet, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        EmitChanges(observer);
    }

    public void EmitChanges(IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
    {
        var changeSet = _groupCache.CaptureChanges();
        if (changeSet.Count != 0)
        {
            var groupChanges =
                changeSet.Select(change =>
                    new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(change.Reason, change.Key, change.Current));

            observer.OnNext(new GroupChangeSet<TObject, TKey, TGroupKey>(groupChanges));
        }
    }

    private ManagedGroup<TObject, TKey, TGroupKey> GetOrAddGroup(TGroupKey groupKey) =>
        _groupCache.Lookup(groupKey).ValueOr(() =>
        {
            var newGroup = new ManagedGroup<TObject, TKey, TGroupKey>(groupKey);
            _groupCache.Add(newGroup, groupKey);
            return newGroup;
        });
}
