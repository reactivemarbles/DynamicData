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

    public IObservable<bool> Run() => Observable.Create<bool>(
            observer =>
            {
                var transformed = _source.Transform(t => new ObservableWithValue<TObject, TValue>(t, _observableSelector(t))).Publish();
                var inlineChanges = transformed.MergeMany(t => t.Observable);
                var queried = transformed.ToCollection();

                // nb: we do not care about the inline change because we are only monitoring it to cause a re-evaluation of all items
                var publisher = queried.CombineLatest(inlineChanges, (items, _) => _collectionMatcher(items)).DistinctUntilChanged().SubscribeSafe(observer);

                return new CompositeDisposable(publisher, transformed.Connect());
            });
}
