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
        private readonly IDictionary<TKey, ItemWithGroup> _itemCache = new Dictionary<TKey, ItemWithGroup>();
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
        private struct ItemWithGroup
        {
            private readonly TGroupKey _groupKey;
            private readonly TObject _item;
            private readonly TKey _key;
            private readonly ChangeReason _reason;

            /// <summary>
            ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
            /// </summary>
            public ItemWithGroup(ChangeReason reason, TObject item, TKey key, TGroupKey groupKey)
            {
                _reason = reason;
                _item = item;
                _key = key;
                _groupKey = groupKey;
            }

            public TObject Item
            {
                get { return _item; }
            }

            public TKey Key
            {
                get { return _key; }
            }

            public TGroupKey GroupKey
            {
                get { return _groupKey; }
            }

            public ChangeReason Reason
            {
                get { return _reason; }
            }

            #region Equality members

            public bool Equals(ItemWithGroup other)
            {
                return EqualityComparer<TKey>.Default.Equals(_key, other._key) &&
                       EqualityComparer<TGroupKey>.Default.Equals(_groupKey, other._groupKey) &&
                       EqualityComparer<TObject>.Default.Equals(_item, other._item);
            }

            /// <summary>
            ///     Indicates whether this instance and a specified object are equal.
            /// </summary>
            /// <returns>
            ///     true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false.
            /// </returns>
            /// <param name="obj">Another object to compare to. </param>
            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is ItemWithGroup && Equals((ItemWithGroup)obj);
            }

            /// <summary>
            ///     Returns the hash code for this instance.
            /// </summary>
            /// <returns>
            ///     A 32-bit signed integer that is the hash code for this instance.
            /// </returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = EqualityComparer<TKey>.Default.GetHashCode(_key);
                    hashCode = (hashCode * 397) ^ EqualityComparer<TGroupKey>.Default.GetHashCode(_groupKey);
                    hashCode = (hashCode * 397) ^ EqualityComparer<TObject>.Default.GetHashCode(_item);
                    return hashCode;
                }
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
                return string.Format("Key: {0}, GroupKey: {1}, Item: {2}", _key, _groupKey, _item);
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
            return HandleUpdates(new ChangeSet<TObject, TKey>(items));
        }

        private GroupChangeSet<TObject, TKey, TGroupKey> HandleUpdates(IChangeSet<TObject, TKey> updates, bool isEvaluating = false)
        {
            var result = new List<Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>>();

            //  var result = new List<GroupUpdate<TObject, TKey, TGroupKey>>();

            //i) evaluate which groups each update should be in 
            List<ItemWithGroup> regularupdates = updates
                .Select(u => new ItemWithGroup(u.Reason, u.Current, u.Key, _groupSelectorKey(u.Current)))
                .ToList();

            //ii) check whether an item has been orpaned from it's original group
            // can happen due to an inline change on the source of the group selector 
            //(captured by Evalue) or an Change. 
            var orphaned =
                regularupdates.Where(u => u.Reason == ChangeReason.Evaluate || u.Reason == ChangeReason.Update)
                    .Select(iwg => new { Current = iwg, Previous = _itemCache.Lookup(iwg.Key) })
                    .Where(x => x.Previous.HasValue && !Equals(x.Current.GroupKey, x.Previous.Value.GroupKey))
                    .GroupBy(x => x.Previous.Value.GroupKey)
                    .ToList();

            //remove orphaned first
            foreach (var item in orphaned)
            {
                var group = item;
                Tuple<ManagedGroup<TObject, TKey, TGroupKey>, bool> cachewithaddflag = GetCache(group.Key);
                ManagedGroup<TObject, TKey, TGroupKey> cache = cachewithaddflag.Item1;

                @group.ForEach(x => _itemCache.Remove(x.Current.Key));
                cache.Update(updater => @group.ForEach(x => updater.Remove(x.Current.Key)));

                if (cache.Count == 0)
                {
                    _groupCache.Remove(group.Key);
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, group.Key,
                        cache));
                }
            }

            //iii) Adds which has resulted from an evealute (i.e. where the grouped item has changed inline)
            IEnumerable<IGrouping<TGroupKey, ItemWithGroup>> addedDueToInlineChanges =
                regularupdates.Where(u => u.Reason == ChangeReason.Evaluate).GroupBy(x => x.GroupKey);
            foreach (var item in addedDueToInlineChanges)
            {
                IGrouping<TGroupKey, ItemWithGroup> group = item;
                Tuple<ManagedGroup<TObject, TKey, TGroupKey>, bool> cachewithaddflag = GetCache(group.Key);
                ManagedGroup<TObject, TKey, TGroupKey> cache = cachewithaddflag.Item1;
                bool added = cachewithaddflag.Item2;
                if (added)
                {
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key,
                        cache));
                    ;
                }
                cache.Update(updater => @group.ForEach(t =>
                {
                    Optional<TObject> existing = updater.Lookup(t.Key);
                    if (!existing.HasValue)
                    {
                        updater.AddOrUpdate(t.Item, t.Key);
                    }
                }));
            }

            //Maintain ItemWithGroup cache
            foreach (ItemWithGroup item in regularupdates)
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

            //regular updates per group
            List<IGrouping<TGroupKey, Change<TObject, TKey>>> groupedUpdates =
                updates.GroupBy(u => _groupSelectorKey(u.Current)).ToList();
            foreach (var item in groupedUpdates)
            {
                //ungroup and update
                IGrouping<TGroupKey, Change<TObject, TKey>> group = item;
                var changeSet = new ChangeSet<TObject, TKey>(group.Select(g => g));

                Tuple<ManagedGroup<TObject, TKey, TGroupKey>, bool> cachewithaddflag = GetCache(group.Key);
                ManagedGroup<TObject, TKey, TGroupKey> cache = cachewithaddflag.Item1;
                bool added = cachewithaddflag.Item2;
                if (added)
                {
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Add, group.Key,
                        cache));
                }
                cache.Update(updater => updater.Update(changeSet));

                if (cache.Count == 0)
                {
                    _groupCache.Remove(group.Key);
                    result.Add(new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(ChangeReason.Remove, group.Key,
                        cache));
                }
            }
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