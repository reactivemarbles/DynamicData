// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Internal;

namespace DynamicData.List.Internal;

internal sealed class Switch<T>(IObservable<IObservable<IChangeSet<T>>> sources)
    where T : notnull
{
    private readonly IObservable<IObservable<IChangeSet<T>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();
                var destination = new SourceList<T>();
                var errors = new Subject<IChangeSet<T>>();
                var innerSubscription = new SerialDisposable();

                // The outer (sources) and every inner are routed through the same SharedDeliveryQueue.
                // Both the per-source clear and the per-changeset destination write happen on the drain
                // thread, so destination.Connect() emissions and any errors.OnError calls also originate
                // from inside the drain. The downstream merge therefore sees pre-serialized inputs and
                // uses UnsynchronizedMerge to avoid the ABBA-prone Observable.Merge gate. Inlines what
                // Observable.Switch would have done (whose own gate would itself be ABBA-prone).
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
