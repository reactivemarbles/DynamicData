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
    /// Synchronizes the source observable through a <see cref="SharedDeliveryQueue"/>.
    /// Use when multiple sources of different types share a gate.
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
    /// Use for single-source operators that need direct access to the queue.
    /// The caller creates the queue (deferred observer) and this method wires
    /// the observer on subscription.
    /// </summary>
    public static IObservable<T> SynchronizeSafe<T>(this IObservable<T> source, DeliveryQueue<T> queue)
    {
        return Observable.Create<T>(observer =>
        {
            queue.SetObserver(observer);

            return source.SubscribeSafe(
                item =>
                {
                    using var scope = queue.AcquireLock();
                    scope.Enqueue(item);
                },
                ex =>
                {
                    using var scope = queue.AcquireLock();
                    scope.EnqueueError(ex);
                },
                () =>
                {
                    using var scope = queue.AcquireLock();
                    scope.EnqueueCompleted();
                });
        });
    }
}