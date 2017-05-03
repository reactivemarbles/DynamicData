using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class DynamicCombiner<T>
    {
        private readonly IObservableList<IObservable<IChangeSet<T>>> _source;
        private readonly CombineOperator _type;
        private readonly object _locker = new object();

        public DynamicCombiner([NotNull] IObservableList<IObservable<IChangeSet<T>>> source, CombineOperator type)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _type = type;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                //this is the resulting list which produces all notifications
                var resultList = new ChangeAwareListWithRefCounts<T>();

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
                                                var notifications = UpdateResultList(sourceLists.Items.AsArray(), resultList, changes);
                                                if (notifications.Count != 0)
                                                    observer.OnNext(notifications);
                                            });

                //when an list is removed, need to 
                var removedItem = sourceLists.Connect()
                                             .OnItemRemoved(mc =>
                                             {
                                                 //Remove items if required
                                                 var notifications = ProcessChanges(sourceLists.Items.AsArray(), resultList, mc.Tracker.Items);
                                                 if (notifications.Count != 0)
                                                     observer.OnNext(notifications);

                                                 if (_type == CombineOperator.And || _type == CombineOperator.Except)
                                                 {
                                                     var itemsToCheck = sourceLists.Items.SelectMany(mc2 => mc2.Tracker.Items).ToArray();
                                                     var notification2 = ProcessChanges(sourceLists.Items.AsArray(), resultList, itemsToCheck);
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
                                                   var notifications = ProcessChanges(sourceLists.Items.AsArray(), resultList, mc.Current.Tracker.Items);
                                                   if (notifications.Count != 0)
                                                       observer.OnNext(notifications);

                                                   if (_type == CombineOperator.And || _type == CombineOperator.Except)
                                                   {
                                                       var notification2 = ProcessChanges(sourceLists.Items.AsArray(), resultList, resultList.ToArray());
                                                       if (notification2.Count != 0)
                                                           observer.OnNext(notification2);
                                                   }
                                               })
                                               .Subscribe();

                return new CompositeDisposable(sourceLists, allChanges, removedItem, sourceChanged);
            });
        }

        private IChangeSet<T> UpdateResultList(MergeContainer[] sourceLists, ChangeAwareListWithRefCounts<T> resultingList, IChangeSet<T> changes)
        {
            //child caches have been updated before we reached this point.
            changes.Flatten().ForEach(change => { ProcessItem(sourceLists, resultingList, change.Current); });
            return resultingList.CaptureChanges();
        }

        private IChangeSet<T> ProcessChanges(MergeContainer[] sourceLists, ChangeAwareListWithRefCounts<T> resultingList, IEnumerable<T> items)
        {
            //check whether the item should be removed from the list
            items.ForEach(item => { ProcessItem(sourceLists, resultingList, item); });
            return resultingList.CaptureChanges();
        }

        private void ProcessItem(MergeContainer[] sourceLists, ChangeAwareListWithRefCounts<T> resultingList, T item)
        {
            //check whether the item should be removed from the list
            var isInResult = resultingList.Contains(item);
            var shouldBeInResult = MatchesConstraint(sourceLists, item);

            if (shouldBeInResult)
            {
                if (!isInResult)
                    resultingList.Add(item);
            }
            else
            {
                if (isInResult)
                    resultingList.Remove(item);
            }
        }

        private bool MatchesConstraint(MergeContainer[] sourceLists, T item)
        {
            if (sourceLists.Length == 0)
                return false;

            switch (_type)
            {
                case CombineOperator.And:
                    {
                        return sourceLists.All(s => s.Tracker.Contains(item));
                    }
                case CombineOperator.Or:
                    {
                        return sourceLists.Any(s => s.Tracker.Contains(item));
                    }
                case CombineOperator.Xor:
                    {
                        return sourceLists.Count(s => s.Tracker.Contains(item)) == 1;
                    }
                case CombineOperator.Except:
                    {
                        var first = sourceLists[0].Tracker.Contains(item);
                        var others = sourceLists.Skip(1).Any(s => s.Tracker.Contains(item));
                        return first && !others;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private class MergeContainer
        {
            public ReferenceCountTracker<T> Tracker { get; } = new ReferenceCountTracker<T>();
            public IObservable<IChangeSet<T>> Source { get; }

            public MergeContainer(IObservable<IChangeSet<T>> source)
            {
                Source = source.Do(Clone);
            }

            private void Clone(IChangeSet<T> changes)
            {
                foreach (var change in changes)
                {
                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            Tracker.Add(change.Item.Current);
                            break;
                        case ListChangeReason.AddRange:
                            foreach (var t in change.Range)
                                Tracker.Add(t);
                            break;
                        case ListChangeReason.Replace:
                            Tracker.Remove(change.Item.Previous.Value);
                            Tracker.Add(change.Item.Current);
                            break;
                        case ListChangeReason.Remove:
                            Tracker.Remove(change.Item.Current);
                            break;
                        case ListChangeReason.RemoveRange:
                        case ListChangeReason.Clear:
                            foreach (var t in change.Range)
                                Tracker.Remove(t);
                            break;
                    }
                }
            }
        }
    }
}
