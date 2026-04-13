// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides SynchronizeSafe extension methods — drop-in replacements
/// for <c>Synchronize(lock)</c> that release the lock before downstream delivery.
/// </summary>
internal static class SynchronizeSafeExtensions
{
    /// <summary>
    /// Synchronizes the source observable through a <see cref="SharedDeliveryQueue"/>.
    /// Use when multiple sources of different types share a gate.
    /// </summary>
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, SharedDeliveryQueue queue)
    {
        return Observable.Create<T>(observer =>
        {
            var subQueue = queue.CreateQueue(observer);
            var sourceSubscription = source.SubscribeSafe(subQueue);

            return Disposable.Create(() =>
            {
                sourceSubscription.Dispose();
                subQueue.Dispose();
            });
        });
    }

    /// <summary>
    /// Synchronizes the source observable through an implicitly created <see cref="DeliveryQueue{T}"/>.
    /// Drop-in replacement for <c>Synchronize(locker)</c>.
    /// </summary>
#if NET9_0_OR_GREATER
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, Lock gate)
#else
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, object gate)
#endif
    {
        return Observable.Create<T>(observer =>
        {
            var queue = new DeliveryQueue<T>(gate, observer);
            return source.Subscribe(queue);
        });
    }

    /// <summary>
    /// Synchronizes the source observable through an implicitly created <see cref="DeliveryQueue{T}"/>,
    /// exposing the queue for callers that need <see cref="DeliveryQueue{T}.EnsureDeliveryComplete"/>
    /// or <see cref="DeliveryQueue{T}.AcquireReadLock"/> during disposal.
    /// </summary>
#if NET9_0_OR_GREATER
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, Lock gate, out DeliveryQueue<T> queue)
#else
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, object gate, out DeliveryQueue<T> queue)
#endif
    {
        // Queue must be created eagerly so the caller can capture the reference.
        // Observer is set lazily when Observable.Create subscribes.
        var q = new DeliveryQueue<T>(gate);
        queue = q;

        return Observable.Create<T>(observer =>
        {
            q.SetObserver(observer);
            return source.Subscribe(q);
        });
    }
}