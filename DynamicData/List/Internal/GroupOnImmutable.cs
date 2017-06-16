using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;


namespace DynamicData.List.Internal
{
    internal sealed class GroupOnImmutable<TObject, TGroupKey>
    {
        private readonly IObservable<IChangeSet<TObject>> _source;
        private readonly Func<TObject, TGroupKey> _groupSelector;
        private readonly IObservable<Unit> _regrouper;

        public GroupOnImmutable([NotNull] IObservable<IChangeSet<TObject>> source, [NotNull] Func<TObject, TGroupKey> groupSelector, IObservable<Unit> regrouper)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _groupSelector = groupSelector ?? throw new ArgumentNullException(nameof(groupSelector));
            _regrouper = regrouper;
        }

        public IObservable<IChangeSet<List.IGrouping<TObject, TGroupKey>>> Run()
        {
            return Observable.Create<IChangeSet<IGrouping<TObject, TGroupKey>>>(observer =>
            {
                var groupings = new ChangeAwareList<IGrouping<TObject, TGroupKey>>();
                var groupCache = new Dictionary<TGroupKey,GroupContainer>();

                //var itemsWithGroup = _source
                //    .Transform(t => new ItemWithValue<TObject, TGroupKey>(t, _groupSelector(t)));

                //capture the grouping up front which has the benefit that the group key is only selected once
                var itemsWithGroup = _source
                    .Transform<TObject, ItemWithGroupKey>((t, previous, idx) =>
                    {
                        return new ItemWithGroupKey(t, _groupSelector(t), previous.Convert(p => p.Group), idx);
                    }, true);

                var locker = new object();
                var shared = itemsWithGroup.Synchronize(locker).Publish();



                var grouper = shared
                    .Select(changes => Process(groupings, groupCache, changes));

                IObservable<IChangeSet<IGrouping<TObject, TGroupKey>>> regrouper;
                if (_regrouper == null)
                {
                    regrouper = Observable.Never<IChangeSet<IGrouping<TObject, TGroupKey>>>();
                }
                else
                {
                    regrouper = _regrouper.Synchronize(locker)
                        .CombineLatest(shared.ToCollection(), (_, collection) => Regroup(groupings, groupCache, collection));
                }

                var publisher = grouper.Merge(regrouper)
                    .NotEmpty()
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }

        private IChangeSet<IGrouping<TObject, TGroupKey>> Regroup(ChangeAwareList<IGrouping<TObject, TGroupKey>> result,
            IDictionary<TGroupKey, GroupContainer> allGroupings,
            IReadOnlyCollection<ItemWithGroupKey> currentItems)
        {
            var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TGroupKey>>();

            foreach (var itemWithValue in currentItems)
            {
                var currentGroupKey = itemWithValue.Group;
                var newGroupKey = _groupSelector(itemWithValue.Item);
                if (newGroupKey.Equals(currentGroupKey)) continue;
                
                //lookup group and if created, add to result set
                var oldGrouping = GetGroup(allGroupings, currentGroupKey);
                if (!initialStateOfGroups.ContainsKey(currentGroupKey))
                    initialStateOfGroups[currentGroupKey] = GetGroupState(oldGrouping);

                //remove from the old group
                oldGrouping.List.Remove(itemWithValue.Item);

                //Mark the old item with the new cache group
                itemWithValue.Group = newGroupKey;

                //add to the new group
                var newGrouping = GetGroup(allGroupings, newGroupKey);
                if (!initialStateOfGroups.ContainsKey(newGroupKey))
                    initialStateOfGroups[newGroupKey] = GetGroupState(newGrouping);

                newGrouping.List.Add(itemWithValue.Item);
            }
            return CreateChangeSet(result, allGroupings, initialStateOfGroups);
        }

        private IChangeSet<IGrouping<TObject, TGroupKey>> Process(ChangeAwareList<IGrouping<TObject, TGroupKey>> result, IDictionary<TGroupKey, GroupContainer> allGroupings, IChangeSet<ItemWithGroupKey> changes)
        {
            //need to keep track of effected groups to calculate correct notifications 
            var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TGroupKey>>();

            foreach (var grouping in changes.Unified().GroupBy(change => change.Current.Group))
            {
                //lookup group and if created, add to result set
                var currentGroup = grouping.Key;
                var groupContainer = GetGroup(allGroupings, currentGroup);

                void GetInitialState()
                {
                    if (!initialStateOfGroups.ContainsKey(grouping.Key))
                        initialStateOfGroups[grouping.Key] = GetGroupState(groupContainer);
                }


                var listToModify = groupContainer.List;

                //iterate through the group's items and process
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
                            var previousGroup = change.Current.PrevousGroup.Value;
                            var currentItem = change.Current.Item;

                            //check whether an item changing has resulted in a different group
                            if (!previousGroup.Equals(currentGroup))
                            {

                                GetInitialState();
                                //add to new group
                                listToModify.Add(currentItem);

                                //remove from old group
                                allGroupings.Lookup(previousGroup)
                                    .IfHasValue(g =>
                                    {
                                        if (!initialStateOfGroups.ContainsKey(g.Key))
                                            initialStateOfGroups[g.Key] = GetGroupState(g.Key, g.List);

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

                            //check whether an item changing has resulted in a different group
                            if (previousGroup.Equals(currentGroup))
                            {
                                //find and replace
                                var index = listToModify.IndexOf(previousItem);
                                listToModify[index] = change.Current.Item;
                            }
                            else
                            {
                                //add to new group
                                listToModify.Add(change.Current.Item);

                                //remove from old group
                                allGroupings.Lookup(previousGroup)
                                    .IfHasValue(g =>
                                    {
                                        if (!initialStateOfGroups.ContainsKey(g.Key))
                                            initialStateOfGroups[g.Key] = GetGroupState(g.Key, g.List);

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

        private IChangeSet<IGrouping<TObject, TGroupKey>> CreateChangeSet(ChangeAwareList<IGrouping<TObject, TGroupKey>> result, IDictionary<TGroupKey, GroupContainer> allGroupings, IDictionary<TGroupKey, IGrouping<TObject, TGroupKey>> initialStateOfGroups)
        {
            //Now maintain target list
            foreach (var intialGroup in initialStateOfGroups)
            {
                var key = intialGroup.Key;
                var current = allGroupings[intialGroup.Key];

                if (current.List.Count == 0)
                {
                    //remove if empty
                    allGroupings.Remove(key);
                    result.Remove(intialGroup.Value);
                }
                else
                {
                    var currentState = GetGroupState(current);
                    if (intialGroup.Value.Count == 0)
                    {
                        //an add
                        result.Add(currentState);
                    }
                    else
                    {
                        //a replace (or add if the old group has already been removed)
                        result.Replace(intialGroup.Value, currentState);
                    }
                }
            }
            return result.CaptureChanges();
        }

        private IGrouping<TObject,  TGroupKey> GetGroupState(GroupContainer grouping)
        {
            return new ImmutableGroup<TObject,  TGroupKey>(grouping.Key, grouping.List);
        }

        private IGrouping<TObject,  TGroupKey> GetGroupState(TGroupKey key, IList<TObject> list)
        {
            return new ImmutableGroup<TObject,  TGroupKey>(key, list);
        }

        private GroupContainer GetGroup(IDictionary<TGroupKey, GroupContainer> groupCaches, TGroupKey key)
        {
            var cached = groupCaches.Lookup(key);
            if (cached.HasValue)
                return cached.Value;

            var newcache = new GroupContainer(key);
            groupCaches[key] = newcache; 
            return newcache;
        }

        private class GroupContainer
        {
            public IList<TObject> List { get; } = new List<TObject>();
            public TGroupKey Key { get; }

            public GroupContainer(TGroupKey key)
            {
                Key = key;
            }
        }

        private sealed class ItemWithGroupKey : IEquatable<ItemWithGroupKey>
        {
            public TObject Item { get; }
            public TGroupKey Group { get; set; }
            public Optional<TGroupKey> PrevousGroup { get; }
            public int Index { get; }

            public ItemWithGroupKey(TObject item, TGroupKey group, Optional<TGroupKey> prevousGroup, int index)
            {
                Item = item;
                Group = group;
                PrevousGroup = prevousGroup;
                Index = index;
            }

            #region Equality 

            public bool Equals(ItemWithGroupKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return EqualityComparer<TObject>.Default.Equals(Item, other.Item);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is ItemWithGroupKey && Equals((ItemWithGroupKey)obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<TObject>.Default.GetHashCode(Item);
            }

            /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.List.Internal.GroupOn`2.ItemWithGroupKey" /> objects are equal.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
            public static bool operator ==(ItemWithGroupKey left, ItemWithGroupKey right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.List.Internal.GroupOn`2.ItemWithGroupKey" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ItemWithGroupKey left, ItemWithGroupKey right)
            {
                return !Equals(left, right);
            }

            #endregion

            /// <summary>
            /// Returns a <see cref="System.String" /> that represents this instance.
            /// </summary>
            /// <returns>
            /// A <see cref="System.String" /> that represents this instance.
            /// </returns>
            public override string ToString()
            {
                return $"{Item} ({Group})";
            }
        }
    }
}