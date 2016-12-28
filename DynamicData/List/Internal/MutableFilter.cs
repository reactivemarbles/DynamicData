using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Internal;

namespace DynamicData.List.Internal
{
    //TODO: Implement seperate ClearAndReplace and CalculateDiffSet filters??
    internal class MutableFilter<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<Func<T, bool>> _predicates;

        private Func<T, bool> _predicate = t => false;

        public MutableFilter([NotNull] IObservable<IChangeSet<T>> source,
                             [NotNull] IObservable<Func<T, bool>> predicates)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicates == null) throw new ArgumentNullException(nameof(predicates));
            _source = source;
            _predicates = predicates;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var allWithMatch = new List<ItemWithMatch>();
                var all = new List<T>();
                var filtered = new ChangeAwareList<T>();
                var locker = new object();

                //requery wehn controller either fires changed or requery event
                var refresher = _predicates.Synchronize(locker)
                                           .Select(predicate =>
                                           {
                                               Requery(predicate, allWithMatch, all, filtered);
                                               var changed = filtered.CaptureChanges();
                                               return changed;
                                           });

                var shared = _source.Synchronize(locker).Publish();

                //take current filter state of all items
                var updateall = shared.Synchronize(locker)
                                      .Transform(t => new ItemWithMatch(t, _predicate(t)))
                                      .Subscribe(allWithMatch.Clone);

                //filter result list
                var filter = shared.Synchronize(locker)
                                   .Select(changes =>
                                   {
                                       filtered.Filter(changes, _predicate);
                                       var changed = filtered.CaptureChanges();
                                       return changed;
                                   });

                var subscriber = refresher.Merge(filter).NotEmpty().SubscribeSafe(observer);

                return new CompositeDisposable(updateall, subscriber, shared.Connect());
            });
        }

        //TODO: Need to account for re-evaluate (as it is not mutually excluse to clear and replace)

        private void Requery(Func<T, bool> predicate, List<ItemWithMatch> allWithMatch, List<T> all, ChangeAwareList<T> filtered)
        {
            _predicate = predicate;

            var newState = allWithMatch.Select(item =>
            {
                var match = _predicate(item.Item);
                var wasMatch = item.IsMatch;

                //reflect filtered state
                if (item.IsMatch != match) item.IsMatch = match;

                return new
                {
                    Item = item,
                    IsMatch = match,
                    WasMatch = wasMatch
                };
            }).ToList();

            //reflect items which are no longer matched
            var noLongerMatched = newState.Where(state => !state.IsMatch && state.WasMatch).Select(state => state.Item.Item);
            filtered.RemoveMany(noLongerMatched);

            //reflect new matches in the list
            var newMatched = newState.Where(state => state.IsMatch && !state.WasMatch).Select(state => state.Item.Item);
            filtered.AddRange(newMatched);
        }

        private class ItemWithMatch
        {
            public T Item { get; }
            public bool IsMatch { get; set; }

            public ItemWithMatch(T item, bool isMatch)
            {
                Item = item;
                IsMatch = isMatch;
            }
        }
    }
}
