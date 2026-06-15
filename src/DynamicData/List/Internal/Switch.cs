// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.List.Internal;

internal sealed class Switch<T>(IObservable<IObservable<IChangeSet<T>>> sources)
    where T : notnull
{
    private readonly IObservable<IObservable<IChangeSet<T>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var destination = new SourceList<T>();

                // Shared queue serializes the source-of-sources signal (Clear on switch)
                // with the inner changeset stream feeding the destination, without holding
                // a lock during destination.Edit (which would otherwise nest with the
                // destination's own queue and risk cross-cache deadlock).
                var queue = new SharedDeliveryQueue();

                var populator = Observable.Switch(
                    _sources.SynchronizeSafe(queue).Do(
                        _ => destination.Clear())).SynchronizeSafe(queue).PopulateInto(destination);

                var publisher = destination.Connect().SubscribeSafe(observer);

                return new CompositeDisposable(destination, populator, publisher, queue);
            });
}
