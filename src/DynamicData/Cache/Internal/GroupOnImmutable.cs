// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the GroupOnImmutable class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="groupSelectorKey">The groupSelectorKey value.</param>
/// <param name="regrouper">The regrouper value.</param>
internal sealed class GroupOnImmutable<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The _groupSelectorKey field.
    /// </summary>
    private readonly Func<TObject, TGroupKey> _groupSelectorKey = groupSelectorKey ?? throw new ArgumentNullException(nameof(groupSelectorKey));

    /// <summary>
    /// The _regrouper field.
    /// </summary>
    private readonly IObservable<Unit> _regrouper = regrouper ?? Observable.Never<Unit>();

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();
                var grouper = new Grouper(_groupSelectorKey);

                var groups = _source.SynchronizeSafe(queue).Select(grouper.Update).Where(changes => changes.Count != 0);

                var regroup = _regrouper.SynchronizeSafe(queue).Select(_ => grouper.Regroup()).Where(changes => changes.Count != 0);

                return new CompositeDisposable(groups.Merge(regroup).SubscribeSafe(observer), queue);
            });

/// <summary>
/// Provides members for the Grouper class.
/// </summary>
/// <param name="groupSelectorKey">The groupSelectorKey value.</param>
private sealed class Grouper(Func<TObject, TGroupKey> groupSelectorKey)
    {
        /// <summary>
        /// The _allGroupings field.
        /// </summary>
        private readonly Dictionary<TGroupKey, GroupCache> _allGroupings = [];

        /// <summary>
        /// The _itemCache field.
        /// </summary>
        private readonly Dictionary<TKey, ChangeWithGroup> _itemCache = [];

        /// <summary>
        /// Executes the Regroup operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Regroup()
        {
            // re-evaluate all items in the group
            var items = _itemCache.Select(item => new Change<TObject, TKey>(ChangeReason.Refresh, item.Key, item.Value.Item));
            return HandleUpdates(new ChangeSet<TObject, TKey>(items));
        }

        /// <summary>
        /// Executes the Update operation.
        /// </summary>
        /// <param name="updates">The updates value.</param>
        /// <returns>The result of the operation.</returns>
        public IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Update(IChangeSet<TObject, TKey> updates) => HandleUpdates(updates);

        /// <summary>
        /// Executes the CreateMissingKeyException operation.
        /// </summary>
        /// <param name="reason">The reason value.</param>
        /// <param name="key">The key value.</param>
        /// <returns>The result of the operation.</returns>
        private static MissingKeyException CreateMissingKeyException(ChangeReason reason, TKey key)
        {
            var message = $"{key} is missing from previous group on {reason}.{Environment.NewLine}Object type {typeof(TObject)}, Key type {typeof(TKey)}, Group key type {typeof(TGroupKey)}";
            return new MissingKeyException(message);
        }

        /// <summary>
        /// Executes the GetGroupState operation.
        /// </summary>
        /// <param name="grouping">The grouping value.</param>
        /// <returns>The result of the operation.</returns>
        private static ImmutableGroup<TObject, TKey, TGroupKey> GetGroupState(GroupCache grouping) => new(grouping.Key, grouping.Cache);

        /// <summary>
        /// Executes the GetGroupState operation.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <param name="cache">The cache value.</param>
        /// <returns>The result of the operation.</returns>
        private static ImmutableGroup<TObject, TKey, TGroupKey> GetGroupState(TGroupKey key, ICache<TObject, TKey> cache) => new(key, cache);

        /// <summary>
        /// Executes the CreateChangeSet operation.
        /// </summary>
        /// <param name="initialGroupState">The initialGroupState value.</param>
        /// <returns>The result of the operation.</returns>
        private ImmutableGroupChangeSet<TObject, TKey, TGroupKey> CreateChangeSet(IDictionary<TGroupKey, IGrouping<TObject, TKey, TGroupKey>> initialGroupState)
        {
            var result = new List<Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>>();
            foreach (var initialGroup in initialGroupState)
            {
                var key = initialGroup.Key;
                var current = _allGroupings[initialGroup.Key];

                if (current.Cache.Count == 0)
                {
                    _allGroupings.Remove(key);
                    result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, key, initialGroup.Value));
                }
                else
                {
                    var currentState = GetGroupState(current);
                    if (initialGroup.Value.Count == 0)
                    {
                        result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, key, currentState));
                    }
                    else
                    {
                        var previousState = ReactiveUI.Primitives.Optional.Some(initialGroup.Value);
                        result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Update, key, currentState, previousState));
                    }
                }
            }

            return new ImmutableGroupChangeSet<TObject, TKey, TGroupKey>(result);
        }

        /// <summary>
        /// Executes the GetCache operation.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <returns>The result of the operation.</returns>
        private Tuple<GroupCache, bool> GetCache(TGroupKey key)
        {
            var cache = _allGroupings.Lookup(key);
            if (cache.HasValue)
            {
                return Tuple.Create(cache.Value, false);
            }

            var newcache = new GroupCache(key);
            _allGroupings[key] = newcache;
            return Tuple.Create(newcache, true);
        }

        /// <summary>
        /// Executes the HandleUpdates operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        /// <returns>The result of the operation.</returns>
        private ImmutableGroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates(IEnumerable<Change<TObject, TKey>> changes)
        {
            // need to keep track of effected groups to calculate correct notifications
            var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TKey, TGroupKey>>();

            // 1. Group all items
            var grouped = changes.Select(u => new ChangeWithGroup(u, groupSelectorKey)).GroupBy(c => c.GroupKey);

            // 2. iterate and maintain child caches
            grouped.ForEach(
                group =>
                {
                    var groupItem = GetCache(group.Key);
                    var groupCache = groupItem.Item1;
                    var cacheToModify = groupCache.Cache;

                    if (!initialStateOfGroups.ContainsKey(group.Key))
                    {
                        initialStateOfGroups[group.Key] = GetGroupState(groupCache);
                    }

                    // 1. Iterate through group changes and maintain the current group
                    foreach (var current in group)
                    {
                        switch (current.Reason)
                        {
                            case ChangeReason.Add:
                                {
                                    cacheToModify.AddOrUpdate(current.Item, current.Key);
                                    _itemCache[current.Key] = current;
                                    break;
                                }

                            case ChangeReason.Update:
                                {
                                    cacheToModify.AddOrUpdate(current.Item, current.Key);

                                    // check whether the previous item was in a different group. If so remove from old group
                                    var previous = _itemCache.Lookup(current.Key).ValueOrThrow(() => CreateMissingKeyException(ChangeReason.Update, current.Key));

                                    if (!previous.GroupKey.Equals(current.GroupKey))
                                    {
                                        RemoveFromOldGroup(initialStateOfGroups, previous.GroupKey, current.Key);
                                    }

                                    _itemCache[current.Key] = current;
                                    break;
                                }

                            case ChangeReason.Remove:
                                {
                                    var existing = cacheToModify.Lookup(current.Key);
                                    if (existing.HasValue)
                                    {
                                        cacheToModify.Remove(current.Key);
                                    }
                                    else
                                    {
                                        // this has been removed due to an underlying evaluate resulting in a remove
                                        var previousGroupKey = _itemCache.Lookup(current.Key).ValueOrThrow(() => CreateMissingKeyException(ChangeReason.Remove, current.Key)).GroupKey;

                                        RemoveFromOldGroup(initialStateOfGroups, previousGroupKey, current.Key);
                                    }

                                    _itemCache.Remove(current.Key);

                                    break;
                                }

                            case ChangeReason.Refresh:
                                {
                                    // check whether the previous item was in a different group. If so remove from old group
                                    var previous = _itemCache.Lookup(current.Key);

                                    previous.IfHasValue(
                                        p =>
                                        {
                                            if (p.GroupKey.Equals(current.GroupKey))
                                            {
                                                return;
                                            }

                                            RemoveFromOldGroup(initialStateOfGroups, p.GroupKey, current.Key);

                                            // add to new group because the group value has changed
                                            cacheToModify.AddOrUpdate(current.Item, current.Key);
                                        }).Else(
                                        () =>
                                            cacheToModify.AddOrUpdate(current.Item, current.Key)); // must be created due to addition

                                    _itemCache[current.Key] = current;
                                    break;
                                }
                        }
                    }
                });

            // 2. Produce and fire notifications [compare current and previous state]
            return CreateChangeSet(initialStateOfGroups);
        }

        /// <summary>
        /// Executes the RemoveFromOldGroup operation.
        /// </summary>
        /// <param name="groupState">The groupState value.</param>
        /// <param name="groupKey">The groupKey value.</param>
        /// <param name="currentKey">The currentKey value.</param>
        private void RemoveFromOldGroup(Dictionary<TGroupKey, IGrouping<TObject, TKey, TGroupKey>> groupState, TGroupKey groupKey, TKey currentKey) =>
            _allGroupings.Lookup(groupKey).IfHasValue(
                g =>
                {
                    if (!groupState.ContainsKey(g.Key))
                    {
                        groupState[g.Key] = GetGroupState(g.Key, g.Cache);
                    }

                    g.Cache.Remove(currentKey);
                });

/// <summary>
/// Represents the ChangeWithGroup value.
/// </summary>
/// <param name="change">The change value.</param>
/// <param name="keySelector">The keySelector value.</param>
private readonly struct ChangeWithGroup(Change<TObject, TKey> change, Func<TObject, TGroupKey> keySelector) : IEquatable<ChangeWithGroup>
        {
            /// <summary>
            /// Gets the Item value.
            /// </summary>
            public TObject Item { get; } = change.Current;

            /// <summary>
            /// Gets the Key value.
            /// </summary>
            public TKey Key { get; } = change.Key;

            /// <summary>
            /// Gets the GroupKey value.
            /// </summary>
            public TGroupKey GroupKey { get; } = keySelector(change.Current);

            /// <summary>
            /// Gets the Reason value.
            /// </summary>
            public ChangeReason Reason { get; } = change.Reason;

            /// <summary>
            /// Executes the operator operation.
            /// </summary>
            /// <param name="left">The left value.</param>
            /// <param name="right">The right value.</param>
            /// <returns>The result of the operation.</returns>
            public static bool operator ==(in ChangeWithGroup left, in ChangeWithGroup right) =>
                left.Equals(right);

            /// <summary>
            /// Executes the operator operation.
            /// </summary>
            /// <param name="left">The left value.</param>
            /// <param name="right">The right value.</param>
            /// <returns>The result of the operation.</returns>
            public static bool operator !=(in ChangeWithGroup left, in ChangeWithGroup right) =>
                !left.Equals(right);

            /// <summary>
            /// Executes the Equals operation.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns>The result of the operation.</returns>
            public bool Equals(ChangeWithGroup other) => Key.Equals(other.Key);

            /// <summary>
            /// Executes the Equals operation.
            /// </summary>
            /// <param name="obj">The obj value.</param>
            /// <returns>The result of the operation.</returns>
            public override bool Equals(object? obj) => obj is ChangeWithGroup changeGroup && Equals(changeGroup);

            /// <summary>
            /// Executes the GetHashCode operation.
            /// </summary>
            /// <returns>The result of the operation.</returns>
            public override int GetHashCode() => Key.GetHashCode();

            /// <summary>
            /// Executes the ToString operation.
            /// </summary>
            /// <returns>The result of the operation.</returns>
            public override string ToString() => $"Key: {Key}, GroupKey: {GroupKey}, Item: {Item}";
        }

/// <summary>
/// Provides members for the GroupCache class.
/// </summary>
/// <param name="key">The key value.</param>
private sealed class GroupCache(TGroupKey key)
        {
            /// <summary>
            /// Gets the Cache value.
            /// </summary>
            public Cache<TObject, TKey> Cache { get; } = new Cache<TObject, TKey>();

            /// <summary>
            /// Gets the Key value.
            /// </summary>
            public TGroupKey Key { get; } = key;
        }
    }
}
