// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Runtime context exposed to an <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/>
/// via <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}.Initialize"/>. Provides per-key
/// inner observable lifecycle management and a hook to serialize arbitrary observables through the
/// same shared queue as source and inner notifications.
/// </summary>
/// <typeparam name="TKey">Source changeset key type.</typeparam>
/// <typeparam name="TInner">Value type emitted by per-key inner observables.</typeparam>
internal interface ICacheOrchestratorContext<TKey, TInner>
    where TKey : notnull
    where TInner : notnull
{
    /// <summary>
    /// Registers, replaces, or removes the inner observable associated with <paramref name="key"/>.
    /// Pass a non-<see langword="null"/> observable to register or replace; pass <see langword="null"/>
    /// to remove. The supplied observable is automatically routed through the shared delivery queue.
    /// </summary>
    /// <param name="key">The source changeset key whose inner subscription should change.</param>
    /// <param name="observable">The new inner observable, or <see langword="null"/> to remove.</param>
    void Track(TKey key, IObservable<TInner>? observable);

    /// <summary>
    /// Wraps <paramref name="observable"/> with the shared delivery queue's synchronization gate so
    /// that any operators chained downstream (e.g. side-effecting <c>Do</c> calls, time-based
    /// buffering of values that will be re-emitted) run under the same serialization that source
    /// and inner notifications already enjoy.
    /// </summary>
    /// <typeparam name="T">Value type of the observable being serialized.</typeparam>
    /// <param name="observable">The observable to wrap.</param>
    /// <returns>A new observable whose <c>OnNext</c> delivery runs under the orchestrator's queue lock.</returns>
    IObservable<T> Serialize<T>(IObservable<T> observable);

    /// <summary>
    /// Subscribes to an auxiliary observable whose lifetime contributes to the orchestrator's
    /// completion accounting. The downstream stream cannot complete until every auxiliary
    /// subscription either completes or is disposed. Emissions are routed through the shared
    /// delivery queue and dispatched to <paramref name="onNext"/>; errors propagate to the
    /// downstream emitter.
    /// </summary>
    /// <typeparam name="T">Value type emitted by the auxiliary observable.</typeparam>
    /// <param name="observable">The auxiliary observable.</param>
    /// <param name="onNext">Callback invoked under the queue lock for each emission.</param>
    /// <returns>A disposable that cancels the subscription. Disposing decrements completion accounting.</returns>
    IDisposable TrackAuxiliary<T>(IObservable<T> observable, Action<T> onNext);
}
