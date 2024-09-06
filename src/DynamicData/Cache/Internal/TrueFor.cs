// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class TrueFor<TObject, TKey, TValue>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
    where TObject : notnull
    where TKey : notnull
    where TValue : notnull
{
    private readonly Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> _collectionMatcher = collectionMatcher ?? throw new ArgumentNullException(nameof(collectionMatcher));

    private readonly Func<TObject, IObservable<TValue>> _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<bool> Run()
        => Observable.Create<bool>(observer =>
        {
            var itemsWithValues = _source
                .Transform(item => new ObservableWithValue<TObject, TValue>(
                    item: item,
                    source: _observableSelector.Invoke(item)))
                .Publish();

            var subscription = Observable.CombineLatest(
                    // Make sure we subscribe to ALL of the items before we make the first evaluation of the collection, so any values published on-subscription don't trigger a re-evaluation of the matcher method.
                    first: itemsWithValues.MergeMany(item => item.Observable),
                    second: itemsWithValues.ToCollection(),
                    // We don't need to actually look at the changed values, we just need them as a trigger to re-evaluate the matcher method.
                    resultSelector: (_, itemsWithValues) => _collectionMatcher.Invoke(itemsWithValues))
                .DistinctUntilChanged()
                .SubscribeSafe(observer);

            return new CompositeDisposable(
                subscription,
                itemsWithValues.Connect());
        });
}
