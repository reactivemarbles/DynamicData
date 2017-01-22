using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class MutableFilter<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<Func<T, bool>> _predicates;

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
                var locker = new object();
                
                Func<T, bool> predicate = t => false;

                var all = new List<ItemWithMatch>();
                var filtered = new ChangeAwareList<ItemWithMatch>();


                //requery when predicate changes
                var predicateChanged = _predicates.Synchronize(locker)
                    .Select(newPredicate =>
                    {
                        predicate = newPredicate;
                        Requery(predicate, all, filtered);
                        return filtered.CaptureChanges();
                    });
                 
                /*
                 * Apply the transform operator so 'IsMatch' state can be evalutated and captured one time only
                 * This is to eliminate the need to re-apply the predicate when determining whether an item was previously matched
                 */
                var filteredResult = _source.Synchronize(locker)
                    .Transform(t => new ItemWithMatch(t, predicate(t)))
                    .Select(changes =>
                    {
                        all.Clone(changes); //keep track of all changes
                        filtered.Filter(changes, iwm => iwm.IsMatch);  //maintain filtered result
                        return filtered.CaptureChanges();
                    });
                
                return predicateChanged.Merge(filteredResult)
                            .NotEmpty()
                            .Select(changes => changes.Transform(iwm => iwm.Item))
                            .SubscribeSafe(observer);
            });
        }

        private void Requery(Func<T, bool> predicate, List<ItemWithMatch> all, ChangeAwareList<ItemWithMatch> filtered)
        {
            var mutatedMatches = new List<Action>(all.Count);

            var newState = all.Select(item =>
            {
                var match = predicate(item.Item);
                var wasMatch = item.IsMatch;

                //Mutate match - defer until filtered list has been modified
                //[to prevent potential IndexOf failures]
                if (item.IsMatch != match)
                    mutatedMatches.Add(()=> item.IsMatch = match);

                return new
                {
                    Item = item,
                    IsMatch = match,
                    WasMatch = wasMatch
                };
            }).ToList();

            //reflect items which are no longer matched
            var noLongerMatched = newState.Where(state => !state.IsMatch && state.WasMatch).Select(state => state.Item);
            filtered.RemoveMany(noLongerMatched);

            //reflect new matches in the list
            var newMatched = newState.Where(state => state.IsMatch && !state.WasMatch).Select(state => state.Item);
            filtered.AddRange(newMatched);

            //finally apply mutations
            mutatedMatches.ForEach(m => m());
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
