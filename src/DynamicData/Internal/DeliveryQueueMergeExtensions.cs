// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides <c>DeliveryQueueMerge</c>, an Rx extension method that serializes the
/// notifications of every input through a single <see cref="DeliveryQueue{T}"/>
/// and emits them on the downstream observer outside the queue's lock.
/// </summary>
/// <remarks>
/// <para>Drop-in alternative to <see cref="Observable.Merge{TSource}(IObservable{TSource}[])"/>
/// for cross-cache pipelines where the Rx Merge gate, held during downstream delivery,
/// would risk an ABBA cycle. <see cref="DeliveryQueue{T}"/> serializes enqueues across
/// concurrent producers but releases its gate before delivering, so a downstream
/// observer that walks into another cache's writer lock cannot deadlock with this
/// operator's serialization point.</para>
/// <para>Every input must share the same element type. When the inputs have different
/// element types or require operator-private projections invoked inside the queue's
/// drain, use <see cref="SynchronizeSafeExtensions.SynchronizeSafe{T}(IObservable{T}, SharedDeliveryQueue)"/>
/// with a <see cref="SharedDeliveryQueue"/> and finish with
/// <see cref="SynchronizeSafeExtensions.UnsynchronizedMerge{T}(IObservable{T}, IObservable{T}[])"/>.</para>
/// </remarks>
internal static class DeliveryQueueMergeExtensions
{
    /// <summary>
    /// Merges <paramref name="first"/> with <paramref name="others"/> by routing every
    /// source through a single <see cref="DeliveryQueue{T}"/>. Functionally equivalent
    /// to <see cref="Observable.Merge{TSource}(IObservable{TSource}[])"/>: completes
    /// only after every source completes; the first error terminates; subscription
    /// occurs in argument order.
    /// </summary>
    /// <typeparam name="T">The element type, common to every input.</typeparam>
    /// <param name="first">The first input observable.</param>
    /// <param name="others">Additional input observables.</param>
    /// <returns>An observable that emits items from every input, serialized through the queue.</returns>
    public static IObservable<T> DeliveryQueueMerge<T>(this IObservable<T> first, params IObservable<T>[] others) =>
        Observable.Create<T>(observer =>
        {
            var queue = new DeliveryQueue<T>(observer);
            var totalSources = others.Length + 1;
            var subscriptions = new CompositeDisposable(totalSources + 1);
            var pending = totalSources;

            // Each source needs its own inner observer instance because Rx's ObserverBase
            // sets a one-shot stopped flag on the first OnCompleted/OnError; a single shared
            // observer would silently drop terminal notifications from every source after
            // the first. OnNext and OnError forward straight to the queue (the queue's gate
            // serializes concurrent calls); OnCompleted is counter-gated so only the last
            // surviving source's completion terminates the merged stream.
            IObserver<T> CreateInner() =>
                Observer.Create<T>(
                    queue.OnNext,
                    queue.OnError,
                    () =>
                    {
                        if (Interlocked.Decrement(ref pending) == 0)
                        {
                            queue.OnCompleted();
                        }
                    });

            subscriptions.Add(first.SubscribeSafe(CreateInner()));
            foreach (var source in others)
            {
                subscriptions.Add(source.SubscribeSafe(CreateInner()));
            }

            // Subscription first so any terminal notification produced during Rx's disposal
            // cascade still flows through the still-active queue. Queue last as cleanup.
            subscriptions.Add(queue);
            return subscriptions;
        });
}
