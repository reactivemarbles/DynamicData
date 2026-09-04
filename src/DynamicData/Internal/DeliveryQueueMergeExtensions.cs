// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

// Same-type Rx merge that owns a DeliveryQueue<T>. Serializes notifications from
// every input through the queue, which releases its gate before delivering, so
// downstream observers that walk into another cache's writer lock cannot deadlock
// with this operator's serialization point. Used where every input has the same
// element type and no per-input projection is needed inside the drain. When element
// types differ or per-input projections are required, route each input through
// SharedDeliveryQueue with SynchronizeSafe and combine them with UnsynchronizedMerge.
internal static class DeliveryQueueMergeExtensions
{
    // Functionally equivalent to Observable.Merge: completes only after every source
    // completes, the first error terminates, subscription occurs in argument order.
    public static IObservable<T> DeliveryQueueMerge<T>(this IObservable<T> first, params IObservable<T>[] others) =>
        Observable.Create<T>(observer =>
        {
            var queue = new DeliveryQueue<T>(observer);
            var remainingSources = others.Length + 1;
            var subscriptions = new CompositeDisposable(remainingSources + 1);

            subscriptions.Add(first.SubscribeSafe(CreateInner()));
            foreach (var source in others)
            {
                subscriptions.Add(source.SubscribeSafe(CreateInner()));
            }

            // Subscription first so any terminal notification produced during Rx's disposal
            // cascade still flows through the still-active queue. Queue last as cleanup.
            subscriptions.Add(queue);
            return subscriptions;

            // Each source needs its own inner observer instance because Rx's ObserverBase
            // sets a one-shot stopped flag on the first OnCompleted or OnError. A single
            // shared observer would silently drop terminal notifications from every source
            // after the first. OnNext and OnError forward straight to the queue (the queue's
            // gate serializes concurrent calls). OnCompleted is counter-gated so only the
            // last surviving source's completion terminates the merged stream.
            IObserver<T> CreateInner() =>
                Observer.Create<T>(
                    queue.OnNext,
                    queue.OnError,
                    () =>
                    {
                        if (Interlocked.Decrement(ref remainingSources) == 0)
                        {
                            queue.OnCompleted();
                        }
                    });
        });
}
