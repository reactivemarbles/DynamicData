// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides <c>DeliveryQueueMerge</c> extension methods, which combine the
/// <see cref="SharedDeliveryQueue"/> serialization step and the gate-free merge step
/// from <see cref="SynchronizeSafeExtensions.UnsynchronizedMerge{T}"/> into a single
/// operator.
/// </summary>
/// <remarks>
/// <para>The motivation is the cross-cache deadlock that operators like <c>Page</c>,
/// <c>Virtualise</c>, <c>AutoRefresh</c>, <c>Sort</c>, <c>GroupWithImmutableState</c>, and
/// <c>QueryWhenChanged</c> all needed to solve: route every input through a single
/// <see cref="SharedDeliveryQueue"/> so downstream delivery is serialized without holding
/// any operator-level lock, then merge the serialized inputs without re-introducing an
/// <c>Observable.Merge</c> gate that would reconstruct the ABBA cycle.</para>
/// <para>Each overload creates and owns its own <see cref="SharedDeliveryQueue"/>, wraps
/// every source through <see cref="SynchronizeSafeExtensions.SynchronizeSafe{T}(IObservable{T}, SharedDeliveryQueue)"/>,
/// and finally combines the wrapped streams with
/// <see cref="SynchronizeSafeExtensions.UnsynchronizedMerge{T}(IObservable{T}, IObservable{T}[])"/>.
/// The returned <see cref="CompositeDisposable"/> tears down the merge subscription before
/// the queue, so any final terminal notification still flows through the still-active queue.</para>
/// <para>The heterogeneous overloads accept a projection per input. Each projection is
/// applied <em>inside</em> the queue's drain (via <c>SynchronizeSafe(queue).Select(projection)</c>),
/// so stateful projections (paginators, sorters, virtualisers, group trackers) see a single,
/// serialized stream of invocations and may safely mutate operator-private state without
/// additional locking.</para>
/// </remarks>
internal static class DeliveryQueueMergeExtensions
{
    /// <summary>
    /// Merges <paramref name="first"/> with <paramref name="others"/> after routing every
    /// source through a single <see cref="SharedDeliveryQueue"/>.
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

    /// <summary>
    /// Merges two inputs of different element types into a single observable of <typeparamref name="TOut"/>
    /// by routing each input through a single shared <see cref="SharedDeliveryQueue"/> and applying its
    /// projection inside the drain.
    /// </summary>
    /// <typeparam name="T1">Element type of the first input.</typeparam>
    /// <typeparam name="T2">Element type of the second input.</typeparam>
    /// <typeparam name="TOut">Common output element type produced by both projections.</typeparam>
    /// <param name="first">The first input observable.</param>
    /// <param name="firstProjection">Projection applied to each element from <paramref name="first"/>.</param>
    /// <param name="second">The second input observable.</param>
    /// <param name="secondProjection">Projection applied to each element from <paramref name="second"/>.</param>
    /// <returns>An observable emitting the projected items from both inputs, serialized through a shared queue.</returns>
    public static IObservable<TOut> DeliveryQueueMerge<T1, T2, TOut>(
        IObservable<T1> first,
        Func<T1, TOut> firstProjection,
        IObservable<T2> second,
        Func<T2, TOut> secondProjection) =>
        Observable.Create<TOut>(observer =>
        {
            var queue = new SharedDeliveryQueue();
            var firstProjected = first.SynchronizeSafe(queue).Select(firstProjection);
            var secondProjected = second.SynchronizeSafe(queue).Select(secondProjection);

            return new CompositeDisposable(
                firstProjected.UnsynchronizedMerge(secondProjected).SubscribeSafe(observer),
                queue);
        });

    /// <summary>
    /// Three-input variant of the heterogeneous overload.
    /// </summary>
    /// <typeparam name="T1">Element type of the first input.</typeparam>
    /// <typeparam name="T2">Element type of the second input.</typeparam>
    /// <typeparam name="T3">Element type of the third input.</typeparam>
    /// <typeparam name="TOut">Common output element type produced by all projections.</typeparam>
    /// <param name="first">The first input observable.</param>
    /// <param name="firstProjection">Projection applied to each element from <paramref name="first"/>.</param>
    /// <param name="second">The second input observable.</param>
    /// <param name="secondProjection">Projection applied to each element from <paramref name="second"/>.</param>
    /// <param name="third">The third input observable.</param>
    /// <param name="thirdProjection">Projection applied to each element from <paramref name="third"/>.</param>
    /// <returns>An observable emitting the projected items from all three inputs, serialized through a shared queue.</returns>
    public static IObservable<TOut> DeliveryQueueMerge<T1, T2, T3, TOut>(
        IObservable<T1> first,
        Func<T1, TOut> firstProjection,
        IObservable<T2> second,
        Func<T2, TOut> secondProjection,
        IObservable<T3> third,
        Func<T3, TOut> thirdProjection) =>
        Observable.Create<TOut>(observer =>
        {
            var queue = new SharedDeliveryQueue();
            var firstProjected = first.SynchronizeSafe(queue).Select(firstProjection);
            var secondProjected = second.SynchronizeSafe(queue).Select(secondProjection);
            var thirdProjected = third.SynchronizeSafe(queue).Select(thirdProjection);

            return new CompositeDisposable(
                firstProjected.UnsynchronizedMerge(secondProjected, thirdProjected).SubscribeSafe(observer),
                queue);
        });
}