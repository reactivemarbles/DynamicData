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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));

            _source = source;
            _groupSelector = groupSelector;
            _regrouper = regrouper;
        }

        public IObservable<IChangeSet<List.IGrouping<TObject, TGroupKey>>> Run()
        {
            return Observable.Create<IChangeSet<IGrouping<TObject, TGroupKey>>>(observer =>
            {
                var groupings = new ChangeAwareList<IGrouping<TObject, TGroupKey>>();
                var groupCache = new Dictionary<TGroupKey,GroupContainer>();

                var itemsWithGroup = _source
                    .Transform(t => new ItemWithValue<TObject, TGroupKey>(t, _groupSelector(t)));

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
            IReadOnlyCollection<ItemWithValue<TObject, TGroupKey>> currentItems)
        {
            var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TGroupKey>>();

            foreach (var itemWithValue in currentItems)
            {
                var currentGroupKey = itemWithValue.Value;
                var newGroupKey = _groupSelector(itemWithValue.Item);
                if (newGroupKey.Equals(currentGroupKey)) continue;
                
                //lookup group and if created, add to result set
                var oldGrouping = GetGroup(allGroupings, currentGroupKey);
                if (!initialStateOfGroups.ContainsKey(currentGroupKey))
                    initialStateOfGroups[currentGroupKey] = GetGroupState(oldGrouping);

                //remove from the old group
                oldGrouping.List.Remove(itemWithValue.Item);

                //Mark the old item with the new cache group
                itemWithValue.Value = newGroupKey;

                //add to the new group
                var newGrouping = GetGroup(allGroupings, newGroupKey);
                if (!initialStateOfGroups.ContainsKey(newGroupKey))
                    initialStateOfGroups[newGroupKey] = GetGroupState(newGrouping);

                newGrouping.List.Add(itemWithValue.Item);
            }
            return CreateChangeSet(result, allGroupings, initialStateOfGroups);
        }

        private IChangeSet<IGrouping<TObject, TGroupKey>> Process(ChangeAwareList<IGrouping<TObject, TGroupKey>> result, IDictionary<TGroupKey, GroupContainer> allGroupings, IChangeSet<ItemWithValue<TObject, TGroupKey>> changes)
        {
            //need to keep track of effected groups to calculate correct notifications 
            var initialStateOfGroups = new Dictionary<TGroupKey, IGrouping<TObject, TGroupKey>>();
            
            foreach (var grouping in changes.Unified().GroupBy(change => change.Current.Value))
            {
                //lookup group and if created, add to result set
                var currentGroup = grouping.Key;
                var groupContainer = GetGroup(allGroupings, currentGroup);

                if (!initialStateOfGroups.ContainsKey(grouping.Key))
                    initialStateOfGroups[grouping.Key] = GetGroupState(groupContainer);

                var listToModify = groupContainer.List;
                
                //iterate through the group's items and process
                foreach (var change in grouping)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                        {
                            listToModify.Add(change.Current.Item);
                            break;
                        }
                        case ListChangeReason.Replace:
                        {
                            var previousItem = change.Previous.Value.Item;
                            var previousGroup = change.Previous.Value.Value;

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
                            listToModify.Remove(change.Current.Item);
                            break;
                        }
                        case ListChangeReason.Clear:
                        {
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

        //private class GroupWithAddIndicator
        //{
        //    public Group<TObject, TGroupKey> Group { get; }
        //    public bool WasCreated { get; }

        //    public GroupWithAddIndicator(Group<TObject, TGroupKey> @group, bool wasCreated)
        //    {
        //        Group = @group;
        //        WasCreated = wasCreated;
        //    }
        //}
    }
}