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
    internal sealed class GroupOn<TObject, TGroupKey>
    {
        private readonly IObservable<IChangeSet<TObject>> _source;
        private readonly Func<TObject, TGroupKey> _groupSelector;
        private readonly IObservable<Unit> _regrouper;

        public GroupOn([NotNull] IObservable<IChangeSet<TObject>> source, [NotNull] Func<TObject, TGroupKey> groupSelector, IObservable<Unit> regrouper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));

            _source = source;
            _groupSelector = groupSelector;
            _regrouper = regrouper ;
        }

        public IObservable<IChangeSet<IGroup<TObject, TGroupKey>>> Run()
        {
            return Observable.Create<IChangeSet<IGroup<TObject, TGroupKey>>>(observer =>
            {
                var groupings = new ChangeAwareList<IGroup<TObject, TGroupKey>>();
                var groupCache = new Dictionary<TGroupKey, Group<TObject, TGroupKey>>();

                var itemsWithGroup = _source
                    .Transform(t => new ItemWithValue<TObject, TGroupKey>(t, _groupSelector(t)));
                
                var locker = new object();
                var shared = itemsWithGroup.Synchronize(locker).Publish();

                var grouper = shared
                    .Select(changes => Process(groupings, groupCache, changes));

                IObservable<IChangeSet<IGroup<TObject, TGroupKey>>> regrouper;
                if (_regrouper == null)
                {
                    regrouper = Observable.Never<IChangeSet<IGroup<TObject, TGroupKey>>>();
                }
                else
                {
                   regrouper = _regrouper.Synchronize(locker)
                    .CombineLatest(shared.ToCollection(), (_, collection) => Regroup(groupings, groupCache, collection));
                }

                var publisher = grouper.Merge(regrouper)
                              .DisposeMany() //dispose removes as the grouping is disposable
                              .NotEmpty()
                              .SubscribeSafe(observer);

                return new CompositeDisposable( publisher, shared.Connect());
            });
        }

        private IChangeSet<IGroup<TObject, TGroupKey>> Regroup(ChangeAwareList<IGroup<TObject, TGroupKey>> result,
            IDictionary<TGroupKey, Group<TObject, TGroupKey>> groupCollection,
            IReadOnlyCollection<ItemWithValue<TObject, TGroupKey>> currentItems)
        {
            //TODO: We need to update ItemWithValue>

            foreach (var itemWithValue in currentItems)
            {
                var currentGroupKey = itemWithValue.Value;
                var newGroupKey = _groupSelector(itemWithValue.Item);
                if (newGroupKey.Equals(currentGroupKey)) continue;
                

                //remove from the old group
                var currentGroupLookup = GetCache(groupCollection, currentGroupKey);
                var currentGroupCache = currentGroupLookup.Group;
                currentGroupCache.Edit(innerList=> innerList.Remove(itemWithValue.Item));

                if (currentGroupCache.List.Count == 0)
                {
                    groupCollection.Remove(currentGroupKey);
                    result.Remove(currentGroupCache);
                }

                //Mark the old item with the new cache group
                itemWithValue.Value = newGroupKey;

                //add to the new group
                var newGroupLookup = GetCache(groupCollection, newGroupKey);
                var newGroupCache = newGroupLookup.Group;
                newGroupCache.Edit(innerList => innerList.Add(itemWithValue.Item));

                if (newGroupLookup.WasCreated)
                    result.Add(newGroupCache);

               
            }

            return result.CaptureChanges();
        }

        private IChangeSet<IGroup<TObject, TGroupKey>> Process(ChangeAwareList<IGroup<TObject, TGroupKey>> result, IDictionary<TGroupKey, Group<TObject, TGroupKey>> groupCollection, IChangeSet<ItemWithValue<TObject, TGroupKey>> changes)
        {
            //TODO.This flattened enumerator is inefficient as range operations are lost.
            //maybe can infer within each grouping whether we can regroup i.e. Another enumerator!!!

            foreach (var grouping in changes.Unified().GroupBy(change => change.Current.Value))
            {
                //lookup group and if created, add to result set
                var currentGroup = grouping.Key;
                var lookup = GetCache(groupCollection, currentGroup);
                var groupCache = lookup.Group;

                if (lookup.WasCreated)
                    result.Add(groupCache);

                //start a group edit session, so all changes are batched
                groupCache.Edit(
                    list =>
                    {
                        //iterate through the group's items and process
                        foreach (var change in grouping)
                        {
                            switch (change.Reason)
                            {
                                case ListChangeReason.Add:
                                    {
                                        list.Add(change.Current.Item);
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
                                            var index = list.IndexOf(previousItem);
                                            list[index] = change.Current.Item;
                                        }
                                        else
                                        {
                                            //add to new group
                                            list.Add(change.Current.Item);

                                            //remove from old group
                                            groupCollection.Lookup(previousGroup)
                                                   .IfHasValue(g =>
                                                   {
                                                       g.Edit(oldList => oldList.Remove(previousItem));
                                                       if (g.List.Count != 0) return;
                                                       groupCollection.Remove(g.GroupKey);
                                                       result.Remove(g);
                                                   });
                                        }

                                        break;
                                    }
                                case ListChangeReason.Remove:
                                    {
                                        list.Remove(change.Current.Item);
                                        break;
                                    }
                                case ListChangeReason.Clear:
                                    {
                                        list.Clear();
                                        break;
                                    }
                            }
                        }
                    });

                if (groupCache.List.Count == 0)
                {
                    groupCollection.Remove(groupCache.GroupKey);
                    result.Remove(groupCache);
                }
            }
            return result.CaptureChanges();
        }

        private GroupWithAddIndicator GetCache(IDictionary<TGroupKey, Group<TObject, TGroupKey>> groupCaches, TGroupKey key)
        {
            var cache = groupCaches.Lookup(key);
            if (cache.HasValue)
                return new GroupWithAddIndicator(cache.Value, false);

            var newcache = new Group<TObject, TGroupKey>(key);
            groupCaches[key] = newcache;
            return new GroupWithAddIndicator(newcache, true);
        }

        private class GroupWithAddIndicator
        {
            public Group<TObject, TGroupKey> Group { get; }
            public bool WasCreated { get; }

            public GroupWithAddIndicator(Group<TObject, TGroupKey> @group, bool wasCreated)
            {
                Group = @group;
                WasCreated = wasCreated;
            }
        }
    }
}
