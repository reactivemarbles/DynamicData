// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

// Drop-in replacements for Observable.Synchronize(lock) that release the lock before
// downstream delivery, plus UnsynchronizedMerge for combining streams whose inputs are
// already serialized through the same queue.
//
// Disposal ordering matters. CompositeDisposable disposes in declaration order, and the
// queue and the source subscription have different roles:
//
//   Subscription-first (gate and SDQ overloads): the queue is the IObserver that the
//   source sends notifications to. Disposing the subscription first allows any final
//   terminal notification (OnCompleted or OnError triggered by Rx's disposal cascade
//   or a Finally operator) to flow through the still-active queue. The queue is
//   disposed last as cleanup.
//
//   Queue-first (parameterless overload): used by operators with teardown side effects
//   (DisposeMany, OnBeingRemoved). The queue is terminated first via DeliveryQueue.Dispose,
//   which ensures all in-flight deliveries complete before the subscription is disposed
//   and teardown logic (e.g. disposing removed items) runs. Terminal notifications are
//   not needed because the subscriber is explicitly tearing down.
internal static class SynchronizeSafeExtensions
{
    // Routes the source through a SharedDeliveryQueue. Use when multiple sources of
    // different types share a gate.
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, SharedDeliveryQueue queue) =>
        Observable.Create<T>(observer =>
        {
            var subQueue = queue.CreateQueue(observer);

            // Subscription first: terminal notifications flow through the still-active sub-queue.
            return new CompositeDisposable(source.SubscribeSafe(subQueue), subQueue);
        });

    // Routes the source through an implicitly created DeliveryQueue<T>. Drop-in replacement
    // for Observable.Synchronize(locker).
#if NET9_0_OR_GREATER
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, Lock gate) =>
#else
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, object gate) =>
#endif
        Observable.Create<T>(observer =>
        {
            var queue = new DeliveryQueue<T>(gate, observer);

            // Subscription first: terminal notifications flow through the still-active queue.
            return new CompositeDisposable(source.SubscribeSafe(queue), queue);
        });

    // Routes the source through an implicitly created DeliveryQueue<T> with automatic
    // delivery completion on dispose. The queue is terminated and drained before the
    // source subscription is disposed, ensuring all in-flight notifications are delivered
    // before teardown.
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source) =>
        Observable.Create<T>(observer =>
        {
            var queue = new DeliveryQueue<T>(observer);

            // Queue first: ensures in-flight deliveries complete before teardown side effects run.
            return new CompositeDisposable(queue, source.SubscribeSafe(queue));
        });

    // Merges every input into a single observable without taking any synchronization gate.
    // Functionally equivalent to Observable.Merge: completes only after every source completes,
    // the first error terminates, subscription occurs in argument order.
    //
    // The caller MUST ensure that delivery from every source is already serialized. In this
    // library the precondition is satisfied by routing every source through the same
    // SharedDeliveryQueue via SynchronizeSafe(queue). The shared queue's drain loop guarantees
    // that at most one notification is in flight to the downstream observer at a time, so the
    // additional gate that Observable.Merge would install is redundant.
    //
    // Removing that gate matters in cross-cache pipelines: Observable.Merge holds its private
    // _gate for the entire duration of downstream delivery, and when downstream delivery walks
    // into another cache's writer lock, two such gates on two operators form an ABBA cycle that
    // the queue-drain design is meant to prevent.
    //
    // Without the external serialization precondition, concurrent OnNext calls into the shared
    // observer will race. Do not use as a general-purpose Observable.Merge replacement.
    public static IObservable<T> UnsynchronizedMerge<T>(this IObservable<T> first, params IObservable<T>[] others) =>
        Observable.Create<T>(observer =>
        {
            var remainingSources = others.Length + 1;
            var subscriptions = new CompositeDisposable(remainingSources);
            var terminated = 0;

            subscriptions.Add(first.SubscribeSafe(CreateInner()));
            foreach (var source in others)
            {
                subscriptions.Add(source.SubscribeSafe(CreateInner()));
            }

            return subscriptions;

            // Each source needs its own inner observer instance because Rx's ObserverBase sets
            // a one-shot stopped flag on the first OnCompleted or OnError. A single shared
            // observer would silently drop terminal notifications from every source after the
            // first. The OnNext/OnError/OnCompleted actions close over the shared remainingSources
            // and terminated counters so cross-source coordination still works.
            IObserver<T> CreateInner() => Observer.Create<T>(OnNextSafe, OnErrorSafe, OnCompletedSafe);

            void OnNextSafe(T value)
            {
                if (Volatile.Read(ref terminated) == 0)
                {
                    observer.OnNext(value);
                }
            }

            void OnErrorSafe(Exception error)
            {
                if (Interlocked.Exchange(ref terminated, 1) == 0)
                {
                    observer.OnError(error);
                }
            }

            void OnCompletedSafe()
            {
                if (Interlocked.Decrement(ref remainingSources) == 0 && Interlocked.Exchange(ref terminated, 1) == 0)
                {
                    observer.OnCompleted();
                }
            }
        });
}
