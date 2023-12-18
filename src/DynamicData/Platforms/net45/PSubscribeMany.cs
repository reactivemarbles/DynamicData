// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
using System.Reactive.Disposables;
using System.Reactive.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    internal sealed class PSubscribeMany<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory, ParallelisationOptions parallelisationOptions)
        where TObject : notnull
        where TKey : notnull
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

        private readonly Func<TObject, TKey, IDisposable> _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));

        public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var published = _source.Publish();
                        var subscriptions = published.Transform((t, k) => _subscriptionFactory(t, k), parallelisationOptions).DisposeMany().Subscribe();

                        return new CompositeDisposable(subscriptions, published.SubscribeSafe(observer), published.Connect());
                    });
    }
}

#endif
