// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
}
