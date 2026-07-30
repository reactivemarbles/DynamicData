// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the GroupOnImmutable class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="groupSelector">The groupSelector value.</param>
/// <param name="reGrouper">The reGrouper value.</param>
internal sealed class GroupOnImmutable<TObject, TGroupKey>(IObservable<IChangeSet<TObject>> source, Func<TObject, TGroupKey> groupSelector, IObservable<Unit>? reGrouper)
    where TObject : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The _groupSelector field.
    /// </summary>
    private readonly Func<TObject, TGroupKey> _groupSelector = groupSelector ?? throw new ArgumentNullException(nameof(groupSelector));

    /// <summary>
    /// The _reGrouper field.
    /// </summary>
    private readonly IObservable<Unit>? _reGrouper = reGrouper;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<IGrouping<TObject, TGroupKey>>> Run() => Observable.Create<IChangeSet<IGrouping<TObject, TGroupKey>>>(
            observer =>
            {
                var groupings = new ChangeAwareList<IGrouping<TObject, TGroupKey>>();
                var groupCache = new Dictionary<TGroupKey, GroupContainer>();

                // var itemsWithGroup = _source
                //    .Transform(t => new ItemWithValue<TObject, TGroupKey>(t, _groupSelector(t)));

                // capture the grouping up front which has the benefit that the group key is only selected once
                var itemsWithGroup = _source.Transform<TObject, ItemWithGroupKey>((t, previous) => new ItemWithGroupKey(t, _groupSelector(t), previous.Convert(p => p.Group)), true);

                var locker = InternalEx.NewLock();
                var shared = itemsWithGroup.Synchronize(locker).Publish();

                var grouper = shared.Select(changes => Process(groupings, groupCache, changes));

                var reGroupFunc = _reGrouper is null ?
                    Observable.Never<IChangeSet<IGrouping<TObject, TGroupKey>>>() :
                    _reGrouper.Synchronize(locker).CombineLatest(shared.ToCollection(), (_, collection) => Regroup(groupings, groupCache, collection));

                var publisher = grouper.Merge(reGroupFunc).NotEmpty().SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });

    /// <summary>
    /// Executes the CreateChangeSet operation.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <param name="allGroupings">The allGroupings value.</param>
    /// <param name="initialStateOfGroups">The initialStateOfGroups value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the GetGroup operation.
    /// </summary>
    /// <param name="groupCaches">The groupCaches value.</param>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the GetGroupState operation.
    /// </summary>
    /// <param name="grouping">The grouping value.</param>
    /// <returns>The result of the operation.</returns>
    private static ImmutableGroup<TObject, TGroupKey> GetGroupState(GroupContainer grouping) => new(grouping.Key, grouping.List);

    /// <summary>
    /// Executes the GetGroupState operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="list">The list value.</param>
    /// <returns>The result of the operation.</returns>
    private static ImmutableGroup<TObject, TGroupKey> GetGroupState(TGroupKey key, IList<TObject> list) => new(key, list);

    /// <summary>
    /// Executes the Process operation.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <param name="allGroupings">The allGroupings value.</param>
    /// <param name="changes">The changes value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the Regroup operation.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <param name="allGroupings">The allGroupings value.</param>
    /// <param name="currentItems">The currentItems value.</param>
    /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Provides members for the GroupContainer class.
/// </summary>
/// <param name="key">The key value.</param>
private sealed class GroupContainer(TGroupKey key)
    {
        /// <summary>
        /// Gets the Key value.
        /// </summary>
        public TGroupKey Key { get; } = key;

        /// <summary>
        /// Gets the List value.
        /// </summary>
        public IList<TObject> List { get; } = new List<TObject>();
    }

/// <summary>
/// Provides members for the ItemWithGroupKey class.
/// </summary>
/// <param name="item">The item value.</param>
/// <param name="group">The group value.</param>
/// <param name="previousGroup">The previousGroup value.</param>
private sealed class ItemWithGroupKey(TObject item, TGroupKey group, ReactiveUI.Primitives.Optional<TGroupKey> previousGroup) : IEquatable<ItemWithGroupKey>
    {
        /// <summary>
        /// Gets or sets the Group value.
        /// </summary>
        public TGroupKey Group { get; set; } = group;

        /// <summary>
        /// Gets the Item value.
        /// </summary>
        public TObject Item { get; } = item;

        /// <summary>
        /// Gets the PreviousGroup value.
        /// </summary>
        public ReactiveUI.Primitives.Optional<TGroupKey> PreviousGroup { get; } = previousGroup;

        /// <summary>
        /// Executes the operator operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator ==(ItemWithGroupKey left, ItemWithGroupKey right) => Equals(left, right);

        /// <summary>
        /// Executes the operator operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        /// <returns>The result of the operation.</returns>
        public static bool operator !=(ItemWithGroupKey left, ItemWithGroupKey right) => !Equals(left, right);

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result of the operation.</returns>
        public override bool Equals(object? obj) => obj is ItemWithGroupKey value && Equals(value);

        /// <summary>
        /// Executes the GetHashCode operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override int GetHashCode() => Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item);

        /// <summary>
        /// Executes the ToString operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override string ToString() => $"{Item} ({Group})";
    }
}
