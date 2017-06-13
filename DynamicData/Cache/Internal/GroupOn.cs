using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class GroupOn<TObject, TKey, TGroupKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TGroupKey> _groupSelectorKey;
        private readonly IObservable<Unit> _regrouper;

        public GroupOn(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit> regrouper)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _groupSelectorKey = groupSelectorKey ?? throw new ArgumentNullException(nameof(groupSelectorKey));
            _regrouper = regrouper ?? Observable.Never<Unit>();
        }

        public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run()
        {
            return Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>
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
            private readonly IDictionary<TGroupKey, ManagedGroup<TObject, TKey, TGroupKey>> _groupCache = new Dictionary<TGroupKey, ManagedGroup<TObject, TKey, TGroupKey>>();
            private readonly Func<TObject, TGroupKey> _groupSelectorKey;
            private readonly IDictionary<TKey, ChangeWithGroup> _itemCache = new Dictionary<TKey, ChangeWithGroup>();

            public Grouper(Func<TObject, TGroupKey> groupSelectorKey)
            {
                _groupSelectorKey = groupSelectorKey;
            }

            public IGroupChangeSet<TObject, TKey, TGroupKey> Update(IChangeSet<TObject, TKey> updates)
            {
                return HandleUpdates(updates);
            }

            public IGroupChangeSet<TObject, TKey, TGroupKey> Regroup()
            {
                //re-evaluate all items in the group
                var items = _itemCache.Select(item => new Change<TObject, TKey>(ChangeReason.Refresh, item.Key, item.Value.Item));
                return HandleUpdates(new ChangeSet<TObject, TKey>(items), true);
            }

            private GroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates(IEnumerable<Change<TObject, TKey>> changes, bool isRegrouping = false)
            {
                var result = new List<Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>>();

                //Group all items
                var grouped = changes
                    .Select(u => new ChangeWithGroup(u, _groupSelectorKey))
                    .GroupBy(c => c.GroupKey);

                //1. iterate and maintain child caches (_groupCache)
                //2. maintain which group each item belongs to (_itemCache) 
                grouped.ForEach(group =>
                {
                    var groupItem = GetCache(group.Key);
                    var groupCache = groupItem.Item1;
                    if (groupItem.Item2)
                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key, groupCache));

                    groupCache.Update(groupUpdater =>
                    {
                        foreach (var current in group)
                        {
                            switch (current.Reason)
                            {
                                case ChangeReason.Add:
                                    {
                                        groupUpdater.AddOrUpdate(current.Item, current.Key);
                                        _itemCache[current.Key] = current;
                                        break;
                                    }
                                case ChangeReason.Update:
                                    {
                                        groupUpdater.AddOrUpdate(current.Item, current.Key);

                                        //check whether the previous item was in a different group. If so remove from old group
                                        var previous = _itemCache.Lookup(current.Key)
                                            .ValueOrThrow(() => new MissingKeyException("{0} is missing from previous value on update. Object type {1}, Key type {2}, Group key type {3}"
                                                        .FormatWith(current.Key, typeof(TObject), typeof(TKey),
                                                            typeof(TGroupKey))));

                                        if (previous.GroupKey.Equals(current.GroupKey)) return;

                                        _groupCache.Lookup(previous.GroupKey)
                                                   .IfHasValue(g =>
                                                   {
                                                       g.Update(u => u.Remove(current.Key));
                                                       if (g.Count != 0) return;
                                                       _groupCache.Remove(g.Key);
                                                       result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, g));
                                                   });

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
                                            //this has been removed due to an underlying evaluate resulting in a remove
                                            var previousGroupKey = _itemCache.Lookup(current.Key)
                                                .ValueOrThrow(() => new MissingKeyException("{0} is missing from previous value on remove. Object type {1}, Key type {2}, Group key type {3}"
                                                            .FormatWith(current.Key, typeof(TObject), typeof(TKey), typeof(TGroupKey)))).GroupKey;

                                            _groupCache.Lookup(previousGroupKey)
                                                       .IfHasValue(g =>
                                                       {
                                                           g.Update(u => u.Remove(current.Key));
                                                           if (g.Count != 0) return;
                                                           _groupCache.Remove(g.Key);
                                                           result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, g));
                                                       });
                                        }

                                        //finally, remove the current item from the item cache
                                        _itemCache.Remove(current.Key);

                                        break;
                                    }
                                case ChangeReason.Refresh:
                                    {
                                        //check whether the previous item was in a different group. If so remove from old group
                                        var previous = _itemCache.Lookup(current.Key);

                                        previous.IfHasValue(p =>
                                        {
                                            if (p.GroupKey.Equals(current.GroupKey))
                                            {
                                                //propagate evaluates up the chain
                                                if (!isRegrouping) groupUpdater.Refresh(current.Key);
                                                return;
                                            }

                                            _groupCache.Lookup(p.GroupKey)
                                                       .IfHasValue(g =>
                                                       {
                                                           g.Update(u => u.Remove(current.Key));
                                                           if (g.Count != 0) return;
                                                           _groupCache.Remove(g.Key);
                                                           result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, g.Key, g));
                                                       });

                                            groupUpdater.AddOrUpdate(current.Item, current.Key);
                                        }).Else(() =>
                                        {
                                            //must be created due to addition
                                            groupUpdater.AddOrUpdate(current.Item, current.Key);
                                        });

                                        _itemCache[current.Key] = current;

                                        break;
                                    }
                            }
                        }
                    });

                    if (groupCache.Count != 0) return;

                    _groupCache.RemoveIfContained(group.Key);
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, @group.Key, groupCache));
                });
                return new GroupChangeSet<TObject, TKey, TGroupKey>(result);
            }

            //private Exception CreateMissingKeyException(ChangeReason reason, TKey key)
            //{
            //    var message = $"{key} is missing. The change reason is '{reason}'." +
            //                  $"{Environment.NewLine}Object type {typeof(TObject)}, Key type {typeof(TKey)}";
            //    return new MissingKeyException(message);
            //}


            private Tuple<ManagedGroup<TObject, TKey, TGroupKey>, bool> GetCache(TGroupKey key)
            {
                var cache = _groupCache.Lookup(key);
                if (cache.HasValue)
                    return Tuple.Create(cache.Value, false);

                var newcache = new ManagedGroup<TObject, TKey, TGroupKey>(key);
                _groupCache[key] = newcache;
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
