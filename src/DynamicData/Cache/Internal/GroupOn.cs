// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOn<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    private readonly Func<TObject, TGroupKey> _groupSelectorKey = groupSelectorKey ?? throw new ArgumentNullException(nameof(groupSelectorKey));

    private readonly IObservable<Unit> _regrouper = regrouper ?? Observable.Never<Unit>();

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(
            observer =>
            {
                var locker = new object();
                var grouper = new Grouper(_groupSelectorKey);

                var groups = _source.Finally(observer.OnCompleted).Synchronize(locker).Select(grouper.Update).Where(changes => changes.Count != 0);

                var regroup = _regrouper.Synchronize(locker).Select(_ => grouper.Regroup()).Where(changes => changes.Count != 0);

                var published = groups.Merge(regroup).Publish();
                var subscriber = published.SubscribeSafe(observer);
                var disposer = published.DisposeMany().Subscribe();

                var connected = published.Connect();

                return Disposable.Create(
                    () =>
                    {
                        connected.Dispose();
                        disposer.Dispose();
                        subscriber.Dispose();
                    });
            });

    private sealed class Grouper(Func<TObject, TGroupKey> groupSelectorKey)
    {
        private readonly Dictionary<TGroupKey, ManagedGroup<TObject, TKey, TGroupKey>> _groupCache = [];
        private readonly Dictionary<TKey, ChangeWithGroup> _itemCache = [];

        public IGroupChangeSet<TObject, TKey, TGroupKey> Regroup()
        {
            // re-evaluate all items in the group
            var items = _itemCache.Select(item => new Change<TObject, TKey>(ChangeReason.Refresh, item.Key, item.Value.Item));
            return HandleUpdates(new ChangeSet<TObject, TKey>(items), true);
        }

        public IGroupChangeSet<TObject, TKey, TGroupKey> Update(IChangeSet<TObject, TKey> updates) => HandleUpdates(updates);

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

        private readonly struct ChangeWithGroup(Change<TObject, TKey> change, Func<TObject, TGroupKey> keySelector) : IEquatable<ChangeWithGroup>
        {
            public TObject Item { get; } = change.Current;

            public TKey Key { get; } = change.Key;

            public TGroupKey GroupKey { get; } = keySelector(change.Current);

            public ChangeReason Reason { get; } = change.Reason;

            public static bool operator ==(in ChangeWithGroup left, in ChangeWithGroup right) => left.Equals(right);

            public static bool operator !=(in ChangeWithGroup left, in ChangeWithGroup right) => !left.Equals(right);

            public bool Equals(ChangeWithGroup other) => EqualityComparer<TKey>.Default.Equals(Key, other.Key);

            public override bool Equals(object? obj) => obj is ChangeWithGroup group && Equals(group);

            public override int GetHashCode() => Key.GetHashCode();

            public override string ToString() => $"Key: {Key}, GroupKey: {GroupKey}, Item: {Item}";
        }

        private sealed class SuspendTracker : IDisposable
        {
            private readonly HashSet<TGroupKey> _trackedKeys = [];
            private readonly CompositeDisposable _disposables = [];

            public void Add(ManagedGroup<TObject, TKey, TGroupKey> managedGroup)
            {
                if (_trackedKeys.Add(managedGroup.Key))
                {
                    _disposables.Add(managedGroup.SuspendNotifications());
                }
            }

            public void Dispose() => _disposables.Dispose();
        }
    }
}
