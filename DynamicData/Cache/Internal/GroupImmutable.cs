using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
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
                        .Finally(observer.OnCompleted)
                        .Synchronize(locker)
                        .Select(grouper.Update)
                        .Where(changes => changes.Count != 0);

                    var regroup = _regrouper.Synchronize(locker)
                        .Select(_ => grouper.Regroup())
                        .Where(changes => changes.Count != 0);

                    var published = groups.Merge(regroup).Publish();
                    var subscriber = published.SubscribeSafe(observer);
                    var disposer = published.DisposeMany().Subscribe();

                    var connected = published.Connect();

                    return Disposable.Create(() =>
                    {
                        connected.Dispose();
                        disposer.Dispose();
                        subscriber.Dispose();
                    });
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
                return HandleUpdates(new ChangeSet<TObject, TKey>(items), true);
            }

            private IImmutableGroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates(IEnumerable<Change<TObject, TKey>> changes, bool isRegrouping = false)
            {
                var result = new List<Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>>();

                


                //Group all items
                var grouped = changes
                    .Select(u => new ChangeWithGroup(u, _groupSelectorKey))
                    .GroupBy(c => c.GroupKey)
                    .ToArray();

                ////need to keep track of effected groups since 
                //var addedGroups = new HashSet<TGroupKey>();

                //1. iterate and maintain child caches (_groupCache)
                grouped.ForEach(group =>
                {
                    var groupItem = GetCache(group.Key);
                    bool wasAdded = groupItem.Item2;
                    GroupCache grouping = groupItem.Item1;
                    Cache<TObject, TKey> groupCache = grouping.Cache;

                    var previousState =  new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);

                    //1. Iterate through group changes and maintain the current group
                    foreach (var current in group)
                    {
                        switch (current.Reason)
                        {
                            case ChangeReason.Add:
                            {
                                groupCache.AddOrUpdate(current.Item, current.Key);
                                _itemCache[current.Key] = current;
                                break;
                            }
                            case ChangeReason.Update:
                            {
                                groupCache.AddOrUpdate(current.Item, current.Key);

                                //check whether the previous item was in a different group. If so remove from old group
                                var previous = _itemCache.Lookup(current.Key)
                                    .ValueOrThrow(() => new MissingKeyException("{0} is missing from previous value on update. Object type {1}, Key type {2}, Group key type {3}"
                                        .FormatWith(current.Key, typeof(TObject), typeof(TKey),typeof(TGroupKey))));

                                if (previous.GroupKey.Equals(current.GroupKey)) return;

                                _allGroupings.Lookup(previous.GroupKey)
                                    .IfHasValue(g =>
                                    {
                                        var previousGroupState = new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);
                                        if (g.Cache.Count == 1)
                                        {
                                            //capture state so we can notify
                                            g.Cache.Remove(current.Key);
                                            _allGroupings.Remove(g.Key);
                                            result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, previousGroupState));

                                        }
                                        else
                                        {
                                            //TODO: This is flawed because if many items may have moved grouping and this will result in many updates 
                                            //remove and generate an update
                                            g.Cache.Remove(current.Key);
                                            var updatedState = new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);
                                            result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Update, @group.Key, updatedState, previousState));
                                        }
                                    });

                                _itemCache[current.Key] = current;
                                break;
                            }
                            case ChangeReason.Remove:
                            {
                                var previousInSameGroup = groupCache.Lookup(current.Key);
                                if (previousInSameGroup.HasValue)
                                {
                                    groupCache.Remove(current.Key);
                                }
                                else
                                {
                                    //this has been removed due to an underlying evaluate resulting in a remove
                                    var previousGroupKey = _itemCache.Lookup(current.Key)
                                        .ValueOrThrow(() => new MissingKeyException("{0} is missing from previous value on remove. Object type {1}, Key type {2}, Group key type {3}"
                                            .FormatWith(current.Key, typeof(TObject), typeof(TKey), typeof(TGroupKey)))).GroupKey;

                                    _allGroupings.Lookup(previousGroupKey)
                                        .IfHasValue(g =>
                                        {
                                            var previousGroupState = new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);
                                            if (g.Cache.Count == 1)
                                            {
                                                //capture state so we can notify
                                                g.Cache.Remove(current.Key);
                                                _allGroupings.Remove(g.Key);
                                                result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, previousGroupState));

                                            }
                                            else
                                            {
                                                //TODO: This is flawed because if many items may have moved grouping and this will result in many updates 
                                                //remove and generate an update
                                                g.Cache.Remove(current.Key);
                                                var updatedState = new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);
                                                result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Update, @group.Key, updatedState, previousState));
                                            }
                                        });
                                }

                                //finally, remove the current item from the item cache
                                _itemCache.Remove(current.Key);

                                break;
                            }
                            case ChangeReason.Evaluate:
                            {
                                //check whether the previous item was in a different group. If so remove from old group
                                var previous = _itemCache.Lookup(current.Key);

                                previous.IfHasValue(p =>
                                {
                                    _allGroupings.Lookup(p.GroupKey)
                                        .IfHasValue(g =>
                                        {
                                            var previousGroupState = new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);
                                            if (g.Cache.Count == 1)
                                            {
                                                //capture state so we can notify
                                                g.Cache.Remove(current.Key);
                                                _allGroupings.Remove(g.Key);
                                                result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, previousGroupState));

                                            }
                                            else
                                            {
                                                //TODO: This is flawed because if many items may have moved grouping and this will result in many updates 
                                                //remove and generate an update
                                                g.Cache.Remove(current.Key);
                                                var updatedState = new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);
                                                result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Update, @group.Key, updatedState, previousState));
                                            }
                                        });

                                    groupCache.AddOrUpdate(current.Item, current.Key);
                                }).Else(() =>
                                {
                                    //must be created due to addition
                                    groupCache.AddOrUpdate(current.Item, current.Key);
                                });

                                _itemCache[current.Key] = current;

                                break;
                            }
                        }
                    }

                    //2. Produce and fire notifications [compare current and previous state]
                    var currentState = new ImmutableGroup<TObject, TKey, TGroupKey>(group.Key, groupCache);
                    if (wasAdded)
                    {
                        result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key, currentState));
                    }
                    else
                    {

                        if (groupCache.Count == 0)
                        {
                            //this implies a remove
                            _allGroupings.RemoveIfContained(group.Key);
                            result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, @group.Key, currentState));

                        }
                        else
                        {
                            //this implies an inline change
                            result.Add(new Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Update, @group.Key, currentState, previousState));
                        }
                    }

                });
                return new ImmutableGroupChangeSet<TObject, TKey, TGroupKey>(result);
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
                /// <summary>
                ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
                /// </summary>
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

                /// <summary>
                ///     Returns the fully qualified type name of this instance.
                /// </summary>
                /// <returns>
                ///     A <see cref="T:System.String" /> containing a fully qualified type name.
                /// </returns>
                public override string ToString()
                {
                    return $"Key: {Key}, GroupKey: {GroupKey}, Item: {Item}";
                }
            }
        }
    }
}