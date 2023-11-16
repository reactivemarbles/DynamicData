// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class SubscribeMany<T>(IObservable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
    where T : notnull
{
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    private readonly Func<T, IDisposable> _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var shared = _source.Publish();
                var subscriptions = shared
                    .Transform(t => _subscriptionFactory(t))
                    .DisposeMany()
                    .SubscribeSafe(Observer.Create<IChangeSet<IDisposable>>(
                        onNext: static _ => { },
                        onError: observer.OnError,
                        onCompleted: static () => { }));

                return new CompositeDisposable(subscriptions, shared.SubscribeSafe(observer), shared.Connect());
            });
}
