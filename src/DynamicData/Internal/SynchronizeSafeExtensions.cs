// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides SynchronizeSafe extension methods, drop-in replacements
/// for <c>Synchronize(lock)</c> that release the lock before downstream delivery.
/// </summary>
/// <remarks>
/// <para><strong>Disposal ordering matters.</strong> <c>CompositeDisposable</c> disposes in
/// declaration order. The queue and the source subscription have different roles:</para>
/// <list type="bullet">
///   <item>
///     <term>Subscription-first (gate and SDQ overloads)</term>
///     <description>The queue is the <c>IObserver</c> that the source sends notifications to.
///     Disposing the subscription first allows any final terminal notification (OnCompleted/OnError
///     triggered by Rx's disposal cascade or a <c>Finally</c> operator) to flow through the
///     still-active queue. The queue is disposed last as cleanup.</description>
///   </item>
///   <item>
///     <term>Queue-first (parameterless overload)</term>
///     <description>Used by operators with teardown side effects (DisposeMany, OnBeingRemoved).
///     The queue is terminated first via <see cref="DeliveryQueue{T}.Dispose"/>, which ensures
///     all in-flight deliveries complete before the subscription is disposed and teardown logic
///     (e.g., disposing removed items) runs. Terminal notifications are not needed because
///     the subscriber is explicitly tearing down.</description>
///   </item>
/// </list>
/// </remarks>
internal static class SynchronizeSafeExtensions
{
    /// <summary>
    /// Synchronizes the source observable through a <see cref="SharedDeliveryQueue"/>.
    /// Use when multiple sources of different types share a gate.
    /// </summary>
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, SharedDeliveryQueue queue) =>
        Observable.Create<T>(observer =>
        {
            var subQueue = queue.CreateQueue(observer);

            // Subscription first: terminal notifications flow through the still-active sub-queue
            return new CompositeDisposable(source.SubscribeSafe(subQueue), subQueue);
        });

    /// <summary>
    /// Synchronizes the source observable through an implicitly created <see cref="DeliveryQueue{T}"/>.
    /// Drop-in replacement for <c>Synchronize(locker)</c>.
    /// </summary>
#if NET9_0_OR_GREATER
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, Lock gate) =>
#else
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, object gate) =>
#endif
        Observable.Create<T>(observer =>
        {
            var queue = new DeliveryQueue<T>(gate, observer);

            // Subscription first: terminal notifications flow through the still-active queue
            return new CompositeDisposable(source.SubscribeSafe(queue), queue);
        });

    /// <summary>
    /// Synchronizes the source observable through an implicitly created <see cref="DeliveryQueue{T}"/>
    /// with automatic delivery completion on dispose. The queue is terminated and drained
    /// before the source subscription is disposed, ensuring all in-flight notifications
    /// are delivered before teardown.
    /// </summary>
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source) =>
        Observable.Create<T>(observer =>
        {
            var queue = new DeliveryQueue<T>(observer);

            // Queue first: ensures in-flight deliveries complete before teardown side effects run
            return new CompositeDisposable(queue, source.SubscribeSafe(queue));
        });

    /// <summary>
    /// Merges <paramref name="first"/> with <paramref name="others"/> into a single observable
    /// without taking any synchronization gate. Functionally equivalent to
    /// <see cref="Observable.Merge{TSource}(IObservable{TSource}[])"/>: completes only after
    /// every source completes; the first error terminates; subscription occurs in argument order.
    /// </summary>
    /// <remarks>
    /// <para>The caller MUST ensure that delivery from every source is already serialized.
    /// In this library the precondition is satisfied by routing every source through the
    /// same <see cref="SharedDeliveryQueue"/> via
    /// <see cref="SynchronizeSafe{T}(IObservable{T}, SharedDeliveryQueue)"/>. The shared
    /// queue's drain loop guarantees that at most one notification is in flight to the
    /// downstream observer at a time, so the additional gate that <c>Observable.Merge</c>
    /// would install is redundant.</para>
    /// <para>Removing that gate matters in cross-cache pipelines: <c>Observable.Merge</c>
    /// holds its private <c>_gate</c> for the entire duration of downstream delivery, and
    /// when downstream delivery walks into another cache's writer lock, two such gates on
    /// two operators form an ABBA cycle that the queue-drain design is meant to prevent.</para>
    /// <para>Without the external serialization precondition, concurrent <c>OnNext</c>
    /// calls into the shared observer will race. Do not use as a general-purpose
    /// <c>Observable.Merge</c> replacement.</para>
    /// </remarks>
    public static IObservable<T> UnsynchronizedMerge<T>(this IObservable<T> first, params IObservable<T>[] others) =>
        Observable.Create<T>(observer =>
        {
            var totalSources = others.Length + 1;
            var subscriptions = new CompositeDisposable(totalSources);
            var pending = totalSources;
            var terminated = 0;

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
                if (Interlocked.Decrement(ref pending) == 0 &&
                    Interlocked.Exchange(ref terminated, 1) == 0)
                {
                    observer.OnCompleted();
                }
            }

            var fanOut = Observer.Create<T>(OnNextSafe, OnErrorSafe, OnCompletedSafe);
            subscriptions.Add(first.SubscribeSafe(fanOut));
            foreach (var source in others)
            {
                subscriptions.Add(source.SubscribeSafe(fanOut));
            }

            return subscriptions;
        });
}
