using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal sealed class FastGrouper<TObject, TKey, TGroupKey>
    {
        #region fields

        private readonly IDictionary<TGroupKey, ManagedGroup<TObject, TKey, TGroupKey>> _groupCache =new Dictionary<TGroupKey, ManagedGroup<TObject, TKey, TGroupKey>>();

        private readonly Func<TObject, TGroupKey> _groupSelectorKey;
        private readonly IDictionary<TKey, ChangeWithGroup> _itemCache = new Dictionary<TKey, ChangeWithGroup>();
        private readonly object _locker = new object();

        private struct ChangeWithGroup : IEquatable<ChangeWithGroup>
        {
            private readonly TGroupKey _groupKey;
            private readonly Change<TObject, TKey> _change;
            /// <summary>
            ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
            /// </summary>
            public ChangeWithGroup(Change<TObject, TKey> change, Func<TObject,TGroupKey> keySelector)
            {
                _groupKey = keySelector(change.Current);
                _change = change;
            }

            public Change<TObject, TKey> Change
            {
                get { return _change; }
            }

            public TObject Item
            {
                get { return _change.Current; }
            }

            public TKey Key
            {
                get { return _change.Key; }
            }

            public TGroupKey GroupKey
            {
                get { return _groupKey; }
            }

            public ChangeReason Reason
            {
                get { return _change.Reason; }
            }

            #region Equality members

            public bool Equals(ChangeWithGroup other)
            {
                return _change.Equals(other._change);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is ChangeWithGroup && Equals((ChangeWithGroup) obj);
            }

            public override int GetHashCode()
            {
                return _change.GetHashCode();
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
                return string.Format("Key: {0}, GroupKey: {1}, Item: {2}", Key, _groupKey, Change.Current);
            }
        }

        #endregion

        #region Construction

        public FastGrouper(Func<TObject, TGroupKey> groupSelectorKey)
        {
            _groupSelectorKey = groupSelectorKey;
        }

        #endregion

        #region Construction

        public IGroupChangeSet<TObject, TKey, TGroupKey> Update(IChangeSet<TObject, TKey> updates)
        {
            return HandleUpdates(updates);
        }

        public IGroupChangeSet<TObject, TKey, TGroupKey> Regroup()
        {
            //re-evaluate all items in the group
            var items = _itemCache.Select(item => new Change<TObject, TKey>(ChangeReason.Evaluate, item.Key, item.Value.Item));
            return HandleUpdates(new ChangeSet<TObject, TKey>(items), true);
        }

        private GroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates2(IChangeSet<TObject, TKey> changes, bool isEvaluating = false)
        {
            var result = new List<Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>>();

            //capture each group of changes
            var groupedChanges = changes
                .Select(c => new ChangeWithGroup(c, _groupSelectorKey))
                .GroupBy(c=>c.GroupKey)
                .ToList();

            foreach (var group in groupedChanges)
            {


                    //1. Get child cache and update entire in 1 batch, reporting on any groups to be added or removed
                    var changeSet = new ChangeSet<TObject, TKey>(group.Select(g => g.Change));
                    
                    var cachewithaddflag = GetCache(group.Key);
                    var cache = cachewithaddflag.Item1;
                    bool added = cachewithaddflag.Item2;
                    if (added)
                    {
                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key, cache));
                    }
                    cache.Update(updater => updater.Update(changeSet));
                    if (cache.Count == 0)
                    {
                        _groupCache.Remove(group.Key);
                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, group.Key,
                            cache));
                    }


                //2. Iterate updates and remove any orphaned items from old groups. i.e. the where grouping has change
                //3. Maintain item group
                foreach (ChangeWithGroup item in group)
                {
                    if (item.Reason == ChangeReason.Update || item.Reason==ChangeReason.Evaluate)
                    {
                        ChangeWithGroup item1 = item;
                        _itemCache.Lookup(item.Key)
                            .IfHasValue(previous =>
                                        {
                                            if (previous.GroupKey.Equals(item1.GroupKey))
                                                return;

                                            if (item1.Reason == ChangeReason.Evaluate)
                                            {
                                                var newCacheToAddTo = GetCache(item1.GroupKey);
                                                var newcache = newCacheToAddTo.Item1;

                                                newcache.Update(updater => updater.AddOrUpdate(item1.Item, item1.Key));
                                                if (newCacheToAddTo.Item2)
                                                {
                                                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, item1.GroupKey, newcache));
                                                }
                                            }

                                            _groupCache.Lookup(previous.GroupKey)
                                               .IfHasValue(oldGroup =>
                                               {
                                                   oldGroup.Update(updater => updater.Remove(item1.Key));
                                                   if (oldGroup.Count == 0)
                                                       result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, previous.GroupKey, oldGroup));
                                               });
                                        });
                    }

                    switch (item.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
                        case ChangeReason.Evaluate:
                            _itemCache[item.Key] = item;
                            break;
                        case ChangeReason.Remove:
                            _itemCache.RemoveIfContained(item.Key);
                            break;
                    }
                }
            }
            return new GroupChangeSet<TObject, TKey, TGroupKey>(result);
        }


        private GroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates(IChangeSet<TObject, TKey> updates, bool isEvaluating=false)
        {
            var result = new List<Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>>();

            //i) evaluate which groups each update should be in 
            var regularupdates = updates
                .Select(u => new ChangeWithGroup(u, _groupSelectorKey))
                .ToList();

            //ii) check whether an item has been orpaned from it's original group
            // can happen due to an inline change on the source of the group selector 
            //(captured by Evalue) or an Change. 
            var orphaned =regularupdates.Where(u => u.Reason == ChangeReason.Evaluate || u.Reason == ChangeReason.Update)
                    .Select(iwg => new {Current = iwg, Previous = _itemCache.Lookup(iwg.Key)})
                    .Where(x => x.Previous.HasValue && !Equals(x.Current.GroupKey, x.Previous.Value.GroupKey))
                    .GroupBy(x => x.Previous.Value.GroupKey)
                    .ToList();

            //remove orphaned first
            foreach (var item in orphaned)
            {
                var group = item;
                var cachewithaddflag = GetCache(group.Key);
                var cache = cachewithaddflag.Item1;

                @group.ForEach(x => _itemCache.Remove(x.Current.Key));
                cache.Update(updater => @group.ForEach(x => updater.Remove(x.Current.Key)));

                if (cache.Count == 0)
                {
                    _groupCache.Remove(group.Key);
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, group.Key,cache));
                }
            }

            //iii) Adds which has resulted from an evealute (i.e. where the grouped item has changed inline)
            var addedDueToInlineChanges = regularupdates
                .Where(u => u.Reason == ChangeReason.Evaluate)
                .GroupBy(iwg => iwg.GroupKey);

            foreach (var item in addedDueToInlineChanges)
            {
                var group = item;
                var cachewithaddflag = GetCache(group.Key);
                var cache = cachewithaddflag.Item1;
                bool groupAdded = cachewithaddflag.Item2;
                if (groupAdded)
                {
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key,cache));
                }
                cache.Update(updater => @group.ForEach(t =>
                                                       {
                                                           var existing = updater.Lookup(t.Key);
                                                           if (!existing.HasValue)
                                                           {
                                                               updater.AddOrUpdate(t.Item, t.Key);
                                                           }
                                                       }));
            }

            //Maintain ItemWithGroup cache
            foreach (ChangeWithGroup item in regularupdates)
            {
                switch (item.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                    case ChangeReason.Evaluate:
                        _itemCache[item.Key] = item;
                        break;
                    case ChangeReason.Remove:
                        _itemCache.RemoveIfContained(item.Key);
                        break;
                }
            }

            //if (!isEvaluating)
            //{
                //No need to propagate evaluating events for RefreshGroup() only
                
                //regular updates per group
                var groupedUpdates = regularupdates.GroupBy(u => u.GroupKey).ToList();
                foreach (var item in groupedUpdates)
                {
                    //ungroup and update
                    var group = item;
                    var changeSet = new ChangeSet<TObject, TKey>(group.Select(g => g.Change));

                    var cachewithaddflag = GetCache(group.Key);
                    var cache = cachewithaddflag.Item1;
                    bool added = cachewithaddflag.Item2;
                    if (added)
                    {
                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key, cache));
                    }
                    cache.Update(updater => updater.Update(changeSet));

                    if (cache.Count == 0)
                    {
                        _groupCache.RemoveIfContained(group.Key);
                        result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, group.Key,
                            cache));
                    }
                }
           // }

            return new GroupChangeSet<TObject, TKey, TGroupKey>(result);
        }

        #endregion

        private Tuple<ManagedGroup<TObject, TKey, TGroupKey>, bool> GetCache(TGroupKey key)
        {
            Optional<ManagedGroup<TObject, TKey, TGroupKey>> cache = _groupCache.Lookup(key);
            if (cache.HasValue)
            {
                return Tuple.Create(cache.Value, false);
            }
            var newcache = new ManagedGroup<TObject, TKey, TGroupKey>(key);
            _groupCache[key] = newcache;
            return Tuple.Create(newcache, true);
        }
    }
}