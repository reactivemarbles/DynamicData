// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class Switch<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IObservable<IChangeSet<TObject, TKey>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();
                var destination = new LockFreeObservableCache<TObject, TKey>();
                var errors = new Subject<IChangeSet<TObject, TKey>>();
                var innerSubscription = new SerialDisposable();

                // The outer (sources) and every inner are routed through the same SharedDeliveryQueue.
                // Both the per-source clear and the per-changeset destination write happen on the drain
                // thread, so destination.Connect() emissions and any errors.OnError calls also originate
                // from inside the drain. The downstream merge therefore sees pre-serialized inputs and
                // uses UnsynchronizedMerge to avoid the ABBA-prone Observable.Merge gate.
                var sourcesSubscription = _sources
                    .SynchronizeSafe(queue)
                    .SubscribeSafe(
                        onNext: newSource =>
                        {
                            destination.Clear();
                            innerSubscription.Disposable = newSource
                                .SynchronizeSafe(queue)
                                .SubscribeSafe(
                                    onNext: changes => destination.Edit(updater => updater.Clone(changes)),
                                    onError: errors.OnError);
                        },
                        onError: errors.OnError);

                return new CompositeDisposable(
                    destination,
                    errors,
                    sourcesSubscription,
                    innerSubscription,
                    destination.Connect().UnsynchronizedMerge(errors).SubscribeSafe(observer),
                    queue);
            });
}
