// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the GroupOn class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="groupSelectorKey">The groupSelectorKey value.</param>
/// <param name="regrouper">The regrouper value.</param>
internal sealed class GroupOn<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper)
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
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();
                var grouper = new Grouper(_groupSelectorKey);

                var groups = _source.SynchronizeSafe(queue).Finally(observer.OnCompleted).Select(grouper.Update).Where(changes => changes.Count != 0);

                var regroup = _regrouper.SynchronizeSafe(queue).Select(_ => grouper.Regroup()).Where(changes => changes.Count != 0);

                var published = groups.Merge(regroup).Publish();
                var subscriber = published.SubscribeSafe(observer);
                var disposer = published.DisposeMany().Subscribe();

                var connected = published.Connect();

                return new CompositeDisposable(connected, disposer, subscriber, queue);
            });

/// <summary>
/// Provides members for the Grouper class.
/// </summary>
/// <param name="groupSelectorKey">The groupSelectorKey value.</param>
private sealed class Grouper(Func<TObject, TGroupKey> groupSelectorKey)
    {
        /// <summary>
        /// The _groupCache field.
        /// </summary>
        private readonly Dictionary<TGroupKey, ManagedGroup<TObject, TKey, TGroupKey>> _groupCache = [];

        /// <summary>
        /// The _itemCache field.
        /// </summary>
        private readonly Dictionary<TKey, ChangeWithGroup> _itemCache = [];

        /// <summary>
        /// Executes the Regroup operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IGroupChangeSet<TObject, TKey, TGroupKey> Regroup()
        {
            // re-evaluate all items in the group
            var items = _itemCache.Select(item => new Change<TObject, TKey>(ChangeReason.Refresh, item.Key, item.Value.Item));
            return HandleUpdates(new ChangeSet<TObject, TKey>(items), true);
        }

        /// <summary>
        /// Executes the Update operation.
        /// </summary>
        /// <param name="updates">The updates value.</param>
        /// <returns>The result of the operation.</returns>
        public IGroupChangeSet<TObject, TKey, TGroupKey> Update(IChangeSet<TObject, TKey> updates) => HandleUpdates(updates);

        /// <summary>
        /// Executes the GetCache operation.
        /// </summary>
        /// <param name="key">The key value.</param>
        /// <returns>The result of the operation.</returns>
        private (ManagedGroup<TObject, TKey, TGroupKey> group, bool wasCreated) GetCache(TGroupKey key)
        {
            var cache = _groupCache.Lookup(key);
            if (cache.HasValue)
            {
                return (cache.Value, false);
            }

            var newcache = new ManagedGroup<TObject, TKey, TGroupKey>(key);
            _groupCache[key] = newcache;
            return (newcache, true);
        }

        /// <summary>
        /// Executes the HandleUpdates operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        /// <param name="isRegrouping">The isRegrouping value.</param>
        /// <returns>The result of the operation.</returns>
        private GroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates(IEnumerable<Change<TObject, TKey>> changes, bool isRegrouping = false)
        {
            var result = new List<Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>>();

            // Group all items
            var grouped = changes.Select(u => new ChangeWithGroup(u, groupSelectorKey)).GroupBy(c => c.GroupKey);

            using var suspendTracker = new SuspendTracker();

            // 1. iterate and maintain child caches (_groupCache)
            // 2. maintain which group each item belongs to (_itemCache)
            grouped.ForEach(group =>
                {
                    var groupItem = GetCache(group.Key);
                    var groupCache = groupItem.group;
                    if (groupItem.wasCreated)
                    {
                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key, groupCache));
                    }
                    else
                    {
                        // It wasn't created, so there could be subscribers, so suspend updates until the end
                        suspendTracker.Add(groupCache);
                    }

                    groupCache.Update(
                        groupUpdater =>
                        {
                            foreach (var current in group)
                            {
                                switch (current.Reason)
                                {
                                    case ChangeReason.Add:
                                    case ChangeReason.Update:
                                        {
                                            groupUpdater.AddOrUpdate(current.Item, current.Key);

                                            // check whether the previous item was in a different group. If so remove from old group
                                            var previous = _itemCache.Lookup(current.Key);

                                            if (previous.HasValue && !EqualityComparer<TGroupKey>.Default.Equals(previous.Value.GroupKey, current.GroupKey))
                                            {
                                                _groupCache.Lookup(previous.Value.GroupKey).IfHasValue(
                                                    g =>
                                                    {
                                                        suspendTracker.Add(g);
                                                        g.Update(u => u.Remove(current.Key));
                                                        if (g.Count != 0)
                                                        {
                                                            return;
                                                        }

                                                        _groupCache.Remove(g.Key);
                                                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, g));
                                                    });
                                            }

                                            _itemCache[current.Key] = current;
                                            break;
                                        }

                                    case ChangeReason.Remove:
                                        {
                                            var previousInSameGroup = groupUpdater.Lookup(current.Key);
                                            if (previousInSameGroup.HasValue)
                                            {
                                                groupUpdater.Remove(current.Key);
                                            }
                                            else
                                            {
                                                // this has been removed due to an underlying evaluate resulting in a remove
                                                var previousGroupKey = _itemCache.Lookup(current.Key).ValueOrThrow(() => new MissingKeyException($"{current.Key} is missing from previous value on remove. Object type {typeof(TObject).FullName}, Key type {typeof(TKey).FullName}, Group key type {typeof(TGroupKey).FullName}")).GroupKey;

                                                _groupCache.Lookup(previousGroupKey).IfHasValue(
                                                    g =>
                                                    {
                                                        suspendTracker.Add(g);
                                                        g.Update(u => u.Remove(current.Key));
                                                        if (g.Count != 0)
                                                        {
                                                            return;
                                                        }

                                                        _groupCache.Remove(g.Key);
                                                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, g));
                                                    });
                                            }

                                            // finally, remove the current item from the item cache
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
                                                    if (EqualityComparer<TGroupKey>.Default.Equals(p.GroupKey, current.GroupKey))
                                                    {
                                                        // propagate evaluates up the chain
                                                        if (!isRegrouping)
                                                        {
                                                            groupUpdater.Refresh(current.Key);
                                                        }

                                                        return;
                                                    }

                                                    _groupCache.Lookup(p.GroupKey).IfHasValue(
                                                        g =>
                                                        {
                                                            suspendTracker.Add(g);
                                                            g.Update(u => u.Remove(current.Key));
                                                            if (g.Count != 0)
                                                            {
                                                                return;
                                                            }

                                                            _groupCache.Remove(g.Key);
                                                            result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, g));
                                                        });

                                                    groupUpdater.AddOrUpdate(current.Item, current.Key);
                                                }).Else(
                                                () => groupUpdater.AddOrUpdate(current.Item, current.Key));

                                            _itemCache[current.Key] = current;

                                            break;
                                        }
                                }
                            }
                        });

                    if (groupCache.Count != 0)
                    {
                        return;
                    }

                    _groupCache.RemoveIfContained(@group.Key);
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, @group.Key, groupCache));
                });

            return new GroupChangeSet<TObject, TKey, TGroupKey>(result);
        }

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
            public static bool operator ==(in ChangeWithGroup left, in ChangeWithGroup right) => left.Equals(right);

            /// <summary>
            /// Executes the operator operation.
            /// </summary>
            /// <param name="left">The left value.</param>
            /// <param name="right">The right value.</param>
            /// <returns>The result of the operation.</returns>
            public static bool operator !=(in ChangeWithGroup left, in ChangeWithGroup right) => !left.Equals(right);

            /// <summary>
            /// Executes the Equals operation.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns>The result of the operation.</returns>
            public bool Equals(ChangeWithGroup other) => EqualityComparer<TKey>.Default.Equals(Key, other.Key);

            /// <summary>
            /// Executes the Equals operation.
            /// </summary>
            /// <param name="obj">The obj value.</param>
            /// <returns>The result of the operation.</returns>
            public override bool Equals(object? obj) => obj is ChangeWithGroup group && Equals(group);

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
            private readonly CompositeDisposable _disposables = [];

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
            /// Executes the Dispose operation.
            /// </summary>
            public void Dispose() => _disposables.Dispose();
        }
    }
}
