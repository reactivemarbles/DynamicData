// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class DynamicFilter<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged, IObservable<Unit>? refilterObservable = null, bool suppressEmptyChangeSets = true)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<Func<TObject, bool>> _predicateChanged = predicateChanged ?? throw new ArgumentNullException(nameof(predicateChanged));
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var allData = new Cache<TObject, TKey>();
                var filteredData = new ChangeAwareCache<TObject, TKey>();
                Func<TObject, bool> predicate = _ => false;

                var locker = new object();

                var refresher = LatestPredicateObservable().Synchronize(locker).Select(
                    p =>
                    {
                        // set the local predicate
                        predicate = p;

                        // reapply filter using all data from the cache
                        return filteredData.RefreshFilteredFrom(allData, predicate);
                    });

                var dataChanged = _source.Synchronize(locker).Select(
                    changes =>
                    {
                        // maintain all data [required to re-apply filter]
                        allData.Clone(changes);

                        // maintain filtered data
                        filteredData.FilterChanges(changes, predicate);

                        // get latest changes
                        return filteredData.CaptureChanges();
                    });

                var sourceMerged = refresher.Merge(dataChanged);
                if (suppressEmptyChangeSets)
                {
                    sourceMerged = sourceMerged.NotEmpty();
                }

                return sourceMerged.SubscribeSafe(observer);
            });

    private IObservable<Func<TObject, bool>> LatestPredicateObservable() => Observable.Create<Func<TObject, bool>>(
            observable =>
            {
                Func<TObject, bool> latest = _ => false;

                observable.OnNext(latest);

                var predicateChangedDisposable = _predicateChanged.Subscribe(
                    predicate =>
                    {
                        latest = predicate;
                        observable.OnNext(latest);
                    });

                var reapplier = refilterObservable is null ? Disposable.Empty : refilterObservable.Subscribe(_ => observable.OnNext(latest));

                return new CompositeDisposable(predicateChangedDisposable, reapplier);
            });
}
