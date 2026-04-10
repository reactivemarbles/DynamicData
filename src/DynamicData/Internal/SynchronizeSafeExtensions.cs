// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides SynchronizeSafe extension methods — drop-in replacements
/// for <c>Synchronize(lock)</c> that release the lock before downstream delivery.
/// </summary>
internal static class SynchronizeSafeExtensions
{
    /// <summary>
    /// Synchronizes the source observable through a shared <see cref="SharedDeliveryQueue"/>.
    /// The lock is held only during enqueue; delivery runs outside the lock.
    /// Use this overload when multiple sources of different types share a gate.
    /// </summary>
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, SharedDeliveryQueue queue)
    {
        return Observable.Create<T>(observer =>
        {
            var subQueue = queue.CreateQueue(observer);

            return source.SubscribeSafe(
                item =>
                {
                    using var scope = subQueue.AcquireLock();
                    scope.Enqueue(item);
                },
                ex =>
                {
                    using var scope = subQueue.AcquireLock();
                    scope.EnqueueError(ex);
                },
                () =>
                {
                    using var scope = subQueue.AcquireLock();
                    scope.EnqueueCompleted();
                });
        });
    }

    /// <summary>
    /// Synchronizes the source observable through a typed <see cref="DeliveryQueue{T}"/>.
    /// The lock is held only during enqueue; delivery runs outside the lock via the
    /// queue's delivery callback. Use this for single-source operators that need
    /// direct access to the queue (e.g., for <see cref="DeliveryQueue{T}.AcquireReadLock"/>).
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> subscription. Delivery happens through
    /// the queue's callback, not through Rx composition.</returns>
    public static IDisposable SynchronizeSafe<T>(this IObservable<T> source, DeliveryQueue<Notification<T>> queue)
    {
        return source.SubscribeSafe(
            item =>
            {
                using var scope = queue.AcquireLock();
                scope.Enqueue(Notification<T>.Next(item));
            },
            ex =>
            {
                using var scope = queue.AcquireLock();
                scope.Enqueue(Notification<T>.OnError(ex));
            },
            () =>
            {
                using var scope = queue.AcquireLock();
                scope.Enqueue(Notification<T>.Completed);
            });
    }
}