using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class GroupOnImmutable<TObject, TKey, TGroupKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TGroupKey> _groupSelectorKey;
        private readonly IObservable<Unit> _regrouper;

        public GroupOnImmutable(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit> regrouper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));

            _source = source;
            _groupSelectorKey = groupSelectorKey;
            _regrouper = regrouper ?? Observable.Never<Unit>();
        }

        public IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> Run()
        {
            return Observable.Create<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>>
            (
                observer =>
                {
                    var locker = new object();
                    var grouper = new Grouper(_groupSelectorKey);

                    var groups = _source
                        .Synchronize(locker)
                        .Select(grouper.Update)
                        .Where(changes => changes.Count != 0);

                    var regroup = _regrouper.Synchronize(locker)
                        .Select(_ => grouper.Regroup())
                        .Where(changes => changes.Count != 0);

                    return  groups.Merge(regroup).SubscribeSafe(observer);
                });
        }

        private sealed class Grouper
        {
            private readonly IDictionary<TGroupKey, GroupCache> _allGroupings = new Dictionary<TGroupKey, GroupCache>();
            private readonly Func<TObject, TGroupKey> _groupSelectorKey;
            private readonly IDictionary<TKey, ChangeWithGroup> _itemCache = new Dictionary<TKey, ChangeWithGroup>();

            public Grouper(Func<TObject, TGroupKey> groupSelectorKey)
            {
                _groupSelectorKey = groupSelectorKey;
            }

            public IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Update(IChangeSet<TObject, TKey> updates)
            {
                return HandleUpdates(updates);
            }

            public IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Regroup()
            {
                //re-evaluate all items in the group
                var items = _itemCache.Select(item => new Change<TObject, TKey>(ChangeReason.Evaluate, item.Key, item.Value.Item));
                return HandleUpdates(new ChangeSet<TObject, TKey>(items));
            }

            private IImmutableGroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates(IEnumerable<Change<TObject, TKey>> changes)
            {
                //need to keep track of effected groups to calculate correct notifications 
                var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TKey, TGroupKey>>();

                //1. Group all items
                var grouped = changes
                    .Select(u => new ChangeWithGroup(u, _groupSelectorKey))
                    .GroupBy(c => c.GroupKey);

                //2. iterate and maintain child caches
                grouped.ForEach(group =>
                {
                    var groupItem = GetCache(group.Key);
                    var groupCache = groupItem.Item1;
                    var cacheToModify = groupCache.Cache;

                    if (!initialStateOfGroups.ContainsKey(group.Key))
                        initialStateOfGroups[group.Key] = GetGroupState(groupCache);

                    //1. Iterate through group changes and maintain the current group
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

                                //check whether the previous item was in a different group. If so remove from old group
                                var previous = _itemCache.Lookup(current.Key)
                                    .ValueOrThrow(() => CreateMissingKeyException(ChangeReason.Update, current.Key));

                                if (!previous.GroupKey.Equals(current.GroupKey))
                                    RemoveFromOldGroup(initialStateOfGroups, previous.GroupKey, current.Key);
                          
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
                                    //this has been removed due to an underlying evaluate resulting in a remove
                                    var previousGroupKey = _itemCache.Lookup(current.Key)
                                        .ValueOrThrow(() => CreateMissingKeyException(ChangeReason.Remove, current.Key))
                                        .GroupKey;

                                    RemoveFromOldGroup(initialStateOfGroups, previousGroupKey, current.Key);
                                }
                                _itemCache.Remove(current.Key);

                                break;
                            }
                            case ChangeReason.Evaluate:
                            {
                                //check whether the previous item was in a different group. If so remove from old group
                                var previous = _itemCache.Lookup(current.Key);

                                previous.IfHasValue(p =>
                                {
                                    if (p.GroupKey.Equals(current.GroupKey)) return;
                                    RemoveFromOldGroup(initialStateOfGroups, p.GroupKey, current.Key);
                                    //add to new group because the group value has changed
                                    cacheToModify.AddOrUpdate(current.Item, current.Key);
                                }).Else(() =>
                                {
                                    //must be created due to addition
                                    cacheToModify.AddOrUpdate(current.Item, current.Key);
                                });

                                _itemCache[current.Key] = current;
                                break;
                            }
                        }
                    }
                });

                //2. Produce and fire notifications [compare current and previous state]
                return CreateChangeSet(initialStateOfGroups);
            }

            private Exception CreateMissingKeyException(ChangeReason reason,  TKey key)
            {
                var message = $"{key} is missing from previous group on {reason}." +
                              $"{Environment.NewLine}Object type {typeof(TObject)}, Key type {typeof(TKey)}, Group key type {typeof(TGroupKey)}";
                return new MissingKeyException(message);
            }

            private void RemoveFromOldGroup(IDictionary<TGroupKey, IGrouping<TObject, TKey, TGroupKey>> groupState, TGroupKey groupKey, TKey currentKey)
            {
                _allGroupings.Lookup(groupKey)
                    .IfHasValue(g =>
                    {
                        if (!groupState.ContainsKey(g.Key))
                            groupState[g.Key] = GetGroupState(g.Key, g.Cache);

                        g.Cache.Remove(currentKey);
                    });
            }

            private IImmutableGroupChangeSet<TObject, TKey, TGroupKey> CreateChangeSet(IDictionary<TGroupKey, IGrouping<TObject, TKey, TGroupKey>> initialGroupState)
            {
                var result = new List<Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>>();
                foreach (var intialGroup in initialGroupState)
                {
                    var key = intialGroup.Key;
                    var current = _allGroupings[intialGroup.Key];

                    if (current.Cache.Count == 0)
                    {
                        _allGroupings.Remove(key);
                        result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, key, intialGroup.Value));
                    }
                    else
                    {
                        var currentState = GetGroupState(current);
                        if (intialGroup.Value.Count == 0)
                        {
                            result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, key, currentState));
                        }
                        else
                        {
                            var previousState = Optional.Some(intialGroup.Value);
                            result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Update, key, currentState, previousState));
                        }
                    }
                }
                return new ImmutableGroupChangeSet<TObject, TKey, TGroupKey>(result);
            }

            private IGrouping<TObject, TKey, TGroupKey> GetGroupState(GroupCache grouping)
            {
                return new ImmutableGroup<TObject, TKey, TGroupKey>(grouping.Key, grouping.Cache);
            }

            private IGrouping<TObject, TKey, TGroupKey> GetGroupState(TGroupKey key, ICache<TObject,TKey> cache)
            {
                return new ImmutableGroup<TObject, TKey, TGroupKey>(key, cache);
            }

            private class GroupCache
            {
                public TGroupKey Key { get;  }
                public Cache<TObject, TKey> Cache { get;  }

                public GroupCache(TGroupKey key)
                {
                    Key = key;
                    Cache = new Cache<TObject, TKey>();
                }
            }

            private Tuple<GroupCache, bool> GetCache(TGroupKey key)
            {
                var cache = _allGroupings.Lookup(key);
                if (cache.HasValue)
                    return Tuple.Create(cache.Value, false);

                var newcache = new GroupCache(key);
                _allGroupings[key] = newcache;
                return Tuple.Create(newcache, true);
            }

            private struct ChangeWithGroup : IEquatable<ChangeWithGroup>
            {
                public ChangeWithGroup(Change<TObject, TKey> change, Func<TObject, TGroupKey> keySelector)
                {
                    GroupKey = keySelector(change.Current);
                    Item = change.Current;
                    Key = change.Key;
                    Reason = change.Reason;
                }

                public TObject Item { get; }
                public TKey Key { get; }
                public TGroupKey GroupKey { get; }
                public ChangeReason Reason { get; }

                #region Equality members

                public bool Equals(ChangeWithGroup other)
                {
                    return Key.Equals(other.Key);
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    return obj is ChangeWithGroup && Equals((ChangeWithGroup)obj);
                }

                public override int GetHashCode()
                {
                    return Key.GetHashCode();
                }

                public static bool operator ==(ChangeWithGroup left, ChangeWithGroup right)
                {
                    return left.Equals(right);
                }

                public static bool operator !=(ChangeWithGroup left, ChangeWithGroup right)
                {
                    return !left.Equals(right);
                }

                #endregion

                public override string ToString()
                {
                    return $"Key: {Key}, GroupKey: {GroupKey}, Item: {Item}";
                }
            }
        }
    }
}