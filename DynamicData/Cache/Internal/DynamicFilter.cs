using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class DynamicFilter<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly IObservable<Func<TObject, bool>> _predicateChanged;
        private readonly IObservable<Unit> _refilterObservable;

        public DynamicFilter(IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<Func<TObject, bool>> predicateChanged,
            IObservable<Unit> refilterObservable = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicateChanged = predicateChanged ?? Observable.Never<Func<TObject, bool>>();
            _refilterObservable = refilterObservable ?? Observable.Never<Unit>();
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var allData = new Cache<TObject, TKey>();
                var filteredData = new ChangeAwareCache<TObject, TKey>();
                var updater = new FilteredUpdater<TObject, TKey>(filteredData, x => false);

                var locker = new object();

                var evaluate = _refilterObservable.
                    Synchronize(locker)
                    .Select(_ => Reevaluate(updater, allData.KeyValues));

                var predicateChanged = _predicateChanged
                    .Synchronize(locker)
                    .Select(predicate =>
                    {
                        updater = new FilteredUpdater<TObject, TKey>(filteredData, predicate);
                        return Reevaluate(updater, allData.KeyValues);
                    });

                var dataChanged = _source
                    .Finally(observer.OnCompleted)
                    .Synchronize(locker)
                    .Select(changes =>
                    {
                        allData.Clone(changes);
                        return updater.Update(changes);
                    });

                return predicateChanged.Merge(evaluate).Merge(dataChanged).NotEmpty().SubscribeSafe(observer);
            });
        }

        private IChangeSet<TObject, TKey> Reevaluate(FilteredUpdater<TObject, TKey> updater,
            IEnumerable<KeyValuePair<TKey, TObject>> items)
        {
            var result = updater.Evaluate(items);
            var changes = result.Where(u => u.Reason == ChangeReason.Add || u.Reason == ChangeReason.Remove);
            return new ChangeSet<TObject, TKey>(changes);
        }
    }
}

