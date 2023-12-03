// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class GroupOnImmutable<TObject, TGroupKey>(IObservable<IChangeSet<TObject>> source, Func<TObject, TGroupKey> groupSelector, IObservable<Unit>? reGrouper)
    where TObject : notnull
    where TGroupKey : notnull
{
    private readonly Func<TObject, TGroupKey> _groupSelector = groupSelector ?? throw new ArgumentNullException(nameof(groupSelector));

    private readonly IObservable<Unit>? _reGrouper = reGrouper;

    private readonly IObservable<IChangeSet<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<IGrouping<TObject, TGroupKey>>> Run() => Observable.Create<IChangeSet<IGrouping<TObject, TGroupKey>>>(
            observer =>
            {
                var groupings = new ChangeAwareList<IGrouping<TObject, TGroupKey>>();
                var groupCache = new Dictionary<TGroupKey, GroupContainer>();

                // var itemsWithGroup = _source
                //    .Transform(t => new ItemWithValue<TObject, TGroupKey>(t, _groupSelector(t)));

                // capture the grouping up front which has the benefit that the group key is only selected once
                var itemsWithGroup = _source.Transform<TObject, ItemWithGroupKey>((t, previous) => new ItemWithGroupKey(t, _groupSelector(t), previous.Convert(p => p.Group)), true);

                var locker = new object();
                var shared = itemsWithGroup.Synchronize(locker).Publish();

                var grouper = shared.Select(changes => Process(groupings, groupCache, changes));

                var reGroupFunc = _reGrouper is null ?
                    Observable.Never<IChangeSet<IGrouping<TObject, TGroupKey>>>() :
                    _reGrouper.Synchronize(locker).CombineLatest(shared.ToCollection(), (_, collection) => Regroup(groupings, groupCache, collection));

                var publisher = grouper.Merge(reGroupFunc).NotEmpty().SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });

    private static IChangeSet<IGrouping<TObject, TGroupKey>> CreateChangeSet(ChangeAwareList<IGrouping<TObject, TGroupKey>> result, IDictionary<TGroupKey, GroupContainer> allGroupings, IDictionary<TGroupKey, IGrouping<TObject, TGroupKey>> initialStateOfGroups)
    {
        // Now maintain target list
        foreach (var initialGroup in initialStateOfGroups)
        {
            var key = initialGroup.Key;
            var current = allGroupings[initialGroup.Key];

            if (current.List.Count == 0)
            {
                // remove if empty
                allGroupings.Remove(key);
                result.Remove(initialGroup.Value);
            }
            else
            {
                var currentState = GetGroupState(current);
                if (initialGroup.Value.Count == 0)
                {
                    // an add
                    result.Add(currentState);
                }
                else
                {
                    // a replace (or add if the old group has already been removed)
                    result.Replace(initialGroup.Value, currentState);
                }
            }
        }

        return result.CaptureChanges();
    }

    private static GroupContainer GetGroup(IDictionary<TGroupKey, GroupContainer> groupCaches, TGroupKey key)
    {
        var cached = groupCaches.Lookup(key);
        if (cached.HasValue)
        {
            return cached.Value;
        }

        var newcache = new GroupContainer(key);
        groupCaches[key] = newcache;
        return newcache;
    }

    private static ImmutableGroup<TObject, TGroupKey> GetGroupState(GroupContainer grouping) => new(grouping.Key, grouping.List);

    private static ImmutableGroup<TObject, TGroupKey> GetGroupState(TGroupKey key, IList<TObject> list) => new(key, list);

    private static IChangeSet<IGrouping<TObject, TGroupKey>> Process(ChangeAwareList<IGrouping<TObject, TGroupKey>> result, IDictionary<TGroupKey, GroupContainer> allGroupings, IChangeSet<ItemWithGroupKey> changes)
    {
        // need to keep track of effected groups to calculate correct notifications
        var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TGroupKey>>();

        foreach (var grouping in changes.Unified().GroupBy(change => change.Current.Group))
        {
            // lookup group and if created, add to result set
            var currentGroup = grouping.Key;
            var groupContainer = GetGroup(allGroupings, currentGroup);

            void GetInitialState()
            {
                if (!initialStateOfGroups.ContainsKey(grouping.Key))
                {
                    initialStateOfGroups[grouping.Key] = GetGroupState(groupContainer);
                }
            }

            var listToModify = groupContainer.List;

            // iterate through the group's items and process
            foreach (var change in grouping)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        {
                            GetInitialState();
                            listToModify.Add(change.Current.Item);
                            break;
                        }

                    case ListChangeReason.Refresh:
                        {
                            var previousItem = change.Current.Item;
                            var previousGroup = change.Current.PreviousGroup.Value;
                            var currentItem = change.Current.Item;

                            // check whether an item changing has resulted in a different group
                            if (!previousGroup.Equals(currentGroup))
                            {
                                GetInitialState();

                                // add to new group
                                listToModify.Add(currentItem);

                                // remove from old group
                                allGroupings.Lookup(previousGroup).IfHasValue(
                                    g =>
                                    {
                                        if (!initialStateOfGroups.ContainsKey(g.Key))
                                        {
                                            initialStateOfGroups[g.Key] = GetGroupState(g.Key, g.List);
                                        }

                                        g.List.Remove(previousItem);
                                    });
                            }

                            break;
                        }

                    case ListChangeReason.Replace:
                        {
                            GetInitialState();
                            var previousItem = change.Previous.Value.Item;
                            var previousGroup = change.Previous.Value.Group;

                            // check whether an item changing has resulted in a different group
                            if (previousGroup.Equals(currentGroup))
                            {
                                // find and replace
                                var index = listToModify.IndexOf(previousItem);
                                listToModify[index] = change.Current.Item;
                            }
                            else
                            {
                                // add to new group
                                listToModify.Add(change.Current.Item);

                                // remove from old group
                                allGroupings.Lookup(previousGroup).IfHasValue(
                                    g =>
                                    {
                                        if (!initialStateOfGroups.ContainsKey(g.Key))
                                        {
                                            initialStateOfGroups[g.Key] = GetGroupState(g.Key, g.List);
                                        }

                                        g.List.Remove(previousItem);
                                    });
                            }

                            break;
                        }

                    case ListChangeReason.Remove:
                        {
                            GetInitialState();
                            listToModify.Remove(change.Current.Item);
                            break;
                        }

                    case ListChangeReason.Clear:
                        {
                            GetInitialState();
                            listToModify.Clear();
                            break;
                        }
                }
            }
        }

        return CreateChangeSet(result, allGroupings, initialStateOfGroups);
    }

    private IChangeSet<IGrouping<TObject, TGroupKey>> Regroup(ChangeAwareList<IGrouping<TObject, TGroupKey>> result, IDictionary<TGroupKey, GroupContainer> allGroupings, IReadOnlyCollection<ItemWithGroupKey> currentItems)
    {
        var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TGroupKey>>();

        foreach (var itemWithValue in currentItems)
        {
            var currentGroupKey = itemWithValue.Group;
            var newGroupKey = _groupSelector(itemWithValue.Item);
            if (newGroupKey.Equals(currentGroupKey))
            {
                continue;
            }

            // lookup group and if created, add to result set
            var oldGrouping = GetGroup(allGroupings, currentGroupKey);
            if (!initialStateOfGroups.ContainsKey(currentGroupKey))
            {
                initialStateOfGroups[currentGroupKey] = GetGroupState(oldGrouping);
            }

            // remove from the old group
            oldGrouping.List.Remove(itemWithValue.Item);

            // Mark the old item with the new cache group
            itemWithValue.Group = newGroupKey;

            // add to the new group
            var newGrouping = GetGroup(allGroupings, newGroupKey);
            if (!initialStateOfGroups.ContainsKey(newGroupKey))
            {
                initialStateOfGroups[newGroupKey] = GetGroupState(newGrouping);
            }

            newGrouping.List.Add(itemWithValue.Item);
        }

        return CreateChangeSet(result, allGroupings, initialStateOfGroups);
    }

    private sealed class GroupContainer(TGroupKey key)
    {
        public TGroupKey Key { get; } = key;

        public IList<TObject> List { get; } = new List<TObject>();
    }

    private sealed class ItemWithGroupKey(TObject item, TGroupKey group, Optional<TGroupKey> previousGroup) : IEquatable<ItemWithGroupKey>
    {
        public TGroupKey Group { get; set; } = group;

        public TObject Item { get; } = item;

        public Optional<TGroupKey> PreviousGroup { get; } = previousGroup;

        public static bool operator ==(ItemWithGroupKey left, ItemWithGroupKey right) => Equals(left, right);

        public static bool operator !=(ItemWithGroupKey left, ItemWithGroupKey right) => !Equals(left, right);

        public bool Equals(ItemWithGroupKey? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<TObject>.Default.Equals(Item, other.Item);
        }

        public override bool Equals(object? obj) => obj is ItemWithGroupKey value && Equals(value);

        public override int GetHashCode() => Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item);

        public override string ToString() => $"{Item} ({Group})";
    }
}
