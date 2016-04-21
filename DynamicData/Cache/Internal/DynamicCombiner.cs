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

        //this is the resulting cache which produces all notifications
        private readonly Cache<TObject, TKey> _resultCache = new Cache<TObject, TKey>();

        private readonly IntermediateUpdater<TObject, TKey> _updater;

        private readonly object _locker = new object();

        public DynamicCombiner([NotNull] IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
            _type = type;
            _updater = new IntermediateUpdater<TObject, TKey>(_resultCache);
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
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
                                                UpdateResultList(sourceLists.Items.AsArray(), changes);

                                                var notifications = _updater.AsChangeSet();
                                                if (notifications.Count != 0)
                                                    observer.OnNext(notifications);
                                            });

                //when an list is removed, need to 
                var removedItem = sourceLists.Connect()
                                             .OnItemRemoved(mc =>
                                             {
                                                 //Remove items if required
                                                 ProcessChanges(sourceLists.Items.AsArray(), mc.Cache.KeyValues);

                                                 if (_type == CombineOperator.And || _type == CombineOperator.Except)
                                                 {
                                                     // var itemsToCheck = resultCache.KeyValues.ToArray();
                                                     var itemsToCheck = sourceLists.Items.SelectMany(mc2 => mc2.Cache.KeyValues).ToArray();
                                                     ProcessChanges(sourceLists.Items.AsArray(), itemsToCheck);
                                                 }

                                                 var notifications = _updater.AsChangeSet();
                                                 if (notifications.Count != 0)
                                                     observer.OnNext(notifications);
                                             })
                                             .Subscribe();

                //when an list is added or removed, need to 
                var sourceChanged = sourceLists.Connect()
                                               .WhereReasonsAre(ListChangeReason.Add, ListChangeReason.AddRange)
                                               .ForEachItemChange(mc =>
                                               {
                                                   ProcessChanges(sourceLists.Items.AsArray(), mc.Current.Cache.KeyValues);

                                                   if (_type == CombineOperator.And || _type == CombineOperator.Except)
                                                       ProcessChanges(sourceLists.Items.AsArray(), _resultCache.KeyValues.ToArray());

                                                   var notifications = _updater.AsChangeSet();
                                                   if (notifications.Count != 0)
                                                       observer.OnNext(notifications);
                                               })
                                               .Subscribe();

                return new CompositeDisposable(sourceLists, allChanges, removedItem, sourceChanged);
            });
        }

        private void UpdateResultList(MergeContainer[] sourceLists, IChangeSet<TObject, TKey> changes)
        {
            changes.ForEach(change => { ProcessItem(sourceLists, change.Current, change.Key); });
        }

        private void ProcessChanges(MergeContainer[] sourceLists, IEnumerable<KeyValuePair<TKey, TObject>> items)
        {
            //check whether the item should be removed from the list (or in the case of And, added)
            items.ForEach(item => { ProcessItem(sourceLists, item.Value, item.Key); });
        }

        private void ProcessItem(MergeContainer[] sourceLists, TObject item, TKey key)
        {
            //TODO: Check whether individual items should be updated

            var cached = _updater.Lookup(key);
            var shouldBeInResult = MatchesConstraint(sourceLists, key);

            if (shouldBeInResult)
            {
                if (!cached.HasValue)
                {
                    _updater.AddOrUpdate(item, key);
                }
                else if (!ReferenceEquals(item, cached.Value))
                {
                    _updater.AddOrUpdate(item, key);
                }
            }
            else
            {
                if (cached.HasValue)
                    _updater.Remove(key);
            }
        }

        private bool MatchesConstraint(MergeContainer[] sources, TKey key)
        {
            if (sources.Length == 0)
                return false;

            switch (_type)
            {
                case CombineOperator.And:
                {
                    return sources.All(s => s.Cache.Lookup(key).HasValue);
                }
                case CombineOperator.Or:
                {
                    return sources.Any(s => s.Cache.Lookup(key).HasValue);
                }
                case CombineOperator.Xor:
                {
                    return sources.Count(s => s.Cache.Lookup(key).HasValue) == 1;
                }
                case CombineOperator.Except:
                {
                    bool first = sources.Take(1).Any(s => s.Cache.Lookup(key).HasValue);
                    bool others = sources.Skip(1).Any(s => s.Cache.Lookup(key).HasValue);
                    return first && !others;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private class MergeContainer
        {
            public Cache<TObject, TKey> Cache { get; } = new Cache<TObject, TKey>();

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
