// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides <c>DeliveryQueueMerge</c>, an Rx extension method that combines
/// the <see cref="SharedDeliveryQueue"/> serialization step and the gate-free
/// merge step from <see cref="SynchronizeSafeExtensions.UnsynchronizedMerge{T}"/>
/// into a single operator.
/// </summary>
/// <remarks>
/// <para>Use this when every input is already of the same element type and no per-input
/// projection is needed before the merge; the operator owns the queue lifecycle so the
/// call site reads like an ordinary <see cref="Observable.Merge{TSource}(IObservable{TSource}[])"/>.
/// When the inputs have different element types or require operator-private projections
/// invoked inside the queue's drain, use <see cref="SynchronizeSafeExtensions.SynchronizeSafe{T}(IObservable{T}, SharedDeliveryQueue)"/>
/// and <see cref="SynchronizeSafeExtensions.UnsynchronizedMerge{T}"/> directly so the
/// projections sit inside the serialized section.</para>
/// </remarks>
internal static class DeliveryQueueMergeExtensions
{
    /// <summary>
    /// Merges <paramref name="first"/> with <paramref name="others"/> after routing every
    /// source through a single <see cref="SharedDeliveryQueue"/>. Drop-in alternative to
    /// <see cref="Observable.Merge{TSource}(IObservable{TSource}[])"/> for cross-cache
    /// pipelines where the Rx Merge gate would risk an ABBA cycle.
    /// </summary>
    /// <typeparam name="T">The element type, common to every input.</typeparam>
    /// <param name="first">The first input observable.</param>
    /// <param name="others">Additional input observables.</param>
    /// <returns>An observable that emits items from every input, serialized through a shared queue.</returns>
    public static IObservable<T> DeliveryQueueMerge<T>(this IObservable<T> first, params IObservable<T>[] others) =>
        Observable.Create<T>(observer =>
        {
            var queue = new SharedDeliveryQueue();
            var firstSync = first.SynchronizeSafe(queue);
            var othersSync = new IObservable<T>[others.Length];
            for (var i = 0; i < others.Length; i++)
            {
                othersSync[i] = others[i].SynchronizeSafe(queue);
            }

            return new CompositeDisposable(
                firstSync.UnsynchronizedMerge(othersSync).SubscribeSafe(observer),
                queue);
        });
}