// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class SubscribeMany<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    private readonly Func<TObject, TKey, IDisposable> _subscriptionFactory;

    public SubscribeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
    {
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _subscriptionFactory = (t, _) => subscriptionFactory(t);
    }

    public SubscribeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));
    }

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var published = _source.Publish();
                var subscriptions = published
                    .Transform((t, k) => _subscriptionFactory(t, k))
                    .DisposeMany()
                    .SubscribeSafe(Observer.Create<IChangeSet<IDisposable, TKey>>(
                        onNext: static _ => { },
                        onError: observer.OnError,
                        onCompleted: static () => { }));

                return new CompositeDisposable(subscriptions, published.SubscribeSafe(observer), published.Connect());
            });
}
