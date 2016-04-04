using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal sealed class DynamicCombiner<TObject, TKey>
    {
        private readonly IObservableList<IObservable<IChangeSet<TObject, TKey>>> _source;
        private readonly CombineOperator _type;
        private readonly object _locker = new object();

        public DynamicCombiner([NotNull] IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
            _type = type;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject,TKey>>(observer =>
            {
                //this is the resulting cache which produces all notifications
                 var resultCache = new Cache<TObject, TKey>();

                //Transform to a merge container. 
                //This populates a RefTracker when the original source is subscribed to
                var sourceLists = _source.Connect()
                    .Synchronize(_locker)
                    .Transform(changeset => new MergeContainer(changeset))
                    .AsObservableList();

                //merge the items back together
                var allChanges = sourceLists.Connect()
                    .MergeMany(mc => mc.Source)
                    .Synchronize(_locker)
                    .Subscribe(changes =>
                    {
                        //Populate result list and chck for changes
                        var notifications = UpdateResultList(sourceLists.Items.AsArray(), resultCache, changes);
                        if (notifications.Count != 0)
                            observer.OnNext(notifications);
                    });

                //when an list is removed, need to 
                var removedItem = sourceLists.Connect()
                    .OnItemRemoved(mc =>
                    {
                        //Remove items if required
                        var notifications = ProcessChanges(sourceLists.Items.AsArray(), resultCache, mc.Cache.KeyValues);
                        if (notifications.Count != 0)
                            observer.OnNext(notifications);


                        if (_type == CombineOperator.And || _type == CombineOperator.Except)
                        {
                           // var itemsToCheck = resultCache.KeyValues.ToArray();
                           var itemsToCheck = sourceLists.Items.SelectMany(mc2 => mc2.Cache.KeyValues).ToArray();
                            var notification2 = ProcessChanges(sourceLists.Items.AsArray(), resultCache, itemsToCheck);
                            if (notification2.Count != 0)
                                observer.OnNext(notification2);
                        }


                    })
                    .Subscribe();

                //when an list is added or removed, need to 
                var sourceChanged = sourceLists.Connect()
                    .WhereReasonsAre(ListChangeReason.Add, ListChangeReason.AddRange)
                    .ForEachItemChange(mc =>
                    {
                        var notifications = ProcessChanges(sourceLists.Items.AsArray(), resultCache, mc.Current.Cache.KeyValues);
                        if (notifications.Count != 0)
                            observer.OnNext(notifications);

                        if (_type == CombineOperator.And || _type == CombineOperator.Except)
                        {
                            var notification2 = ProcessChanges(sourceLists.Items.AsArray(), resultCache, resultCache.KeyValues.ToArray());
                            if (notification2.Count != 0)
                                observer.OnNext(notification2);
                        }
                    })
                    .Subscribe();

                return new CompositeDisposable(sourceLists, allChanges, removedItem, sourceChanged);
            });
        }

        private IChangeSet<TObject, TKey> UpdateResultList(MergeContainer[] sourceLists, Cache<TObject, TKey> resultingList, IChangeSet<TObject,TKey> changes)
        {
            //child caches have been updated before we reached this point.
            var updater = new IntermediateUpdater<TObject, TKey>(resultingList);
            changes.ForEach(change =>
            {
                ProcessItem(sourceLists, updater, change.Current, change.Key);
            });
            return updater.AsChangeSet();
        }


        private IChangeSet<TObject, TKey> ProcessChanges(MergeContainer[] sourceLists, Cache<TObject, TKey> resultingList, IEnumerable<KeyValuePair<TKey, TObject>> items)
        {
            //check whether the item should be removed from the list
            var updater = new   IntermediateUpdater<TObject, TKey>(resultingList);
            items.ForEach(item =>
            {
                ProcessItem(sourceLists, updater, item.Value,item.Key);
            });
            return updater.AsChangeSet();
        }

        private void ProcessItem(MergeContainer[] sourceLists, IntermediateUpdater<TObject, TKey> resultingList,TObject item, TKey key)
        {
            //TODO: Check whether individual items should be updated

            var cached = resultingList.Lookup(key);
            var shouldBeInResult = MatchesConstraint(sourceLists, key);

            if (shouldBeInResult)
            {
                if (!cached.HasValue)
                {
                    resultingList.AddOrUpdate(item, key);
                }
                else if (!ReferenceEquals(item, cached.Value))
                {
                    resultingList.AddOrUpdate(item, key);
                }
            }
            else
            {
                if (cached.HasValue)
                    resultingList.Remove(key);
            }
        }

        private bool MatchesConstraint(MergeContainer[] sourceLists, TKey key)
        {
            switch (_type)
            {
                case CombineOperator.And:
                    {
                        return sourceLists.All(s => s.Cache.Lookup(key).HasValue);
                    }
                case CombineOperator.Or:
                    {
                        return sourceLists.Any(s => s.Cache.Lookup(key).HasValue);
                    }
                case CombineOperator.Xor:
                    {
                        return sourceLists.Count(s => s.Cache.Lookup(key).HasValue) == 1;
                    }
                case CombineOperator.Except:
                    {
                        bool first = sourceLists.Take(1).Any(s => s.Cache.Lookup(key).HasValue);
                        bool others = sourceLists.Skip(1).Any(s => s.Cache.Lookup(key).HasValue);
                        return first && !others;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private class MergeContainer
        {
            public  Cache<TObject, TKey> Cache { get; } = new Cache<TObject, TKey>();

            public IObservable<IChangeSet<TObject, TKey>> Source { get; }


            public MergeContainer(IObservable<IChangeSet<TObject, TKey>> source)
            {
                Source = source.Do(Clone);
            }

            private void Clone(IChangeSet<TObject, TKey> changes)
            {
                Cache.Clone(changes);
            }
        }

    }
}