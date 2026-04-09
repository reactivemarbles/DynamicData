// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides the <see cref="SynchronizeSafe{T}"/> extension method — a drop-in replacement
/// for <c>Synchronize(lock)</c> that releases the lock before downstream delivery.
/// </summary>
internal static class SynchronizeSafeExtensions
{
    /// <summary>
    /// Synchronizes the source observable through a shared <see cref="SharedDeliveryQueue"/>.
    /// The lock is held only during enqueue; delivery runs outside the lock.
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
}
