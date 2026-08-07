// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Runtime context exposed to an <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/>
/// via its factory. Provides per-key inner observable lifecycle management and a hook to serialize
/// arbitrary observables through the same shared queue as source and inner notifications.
/// </summary>
/// <typeparam name="TKey">Source changeset key type.</typeparam>
/// <typeparam name="TInner">Value type emitted by per-key inner observables.</typeparam>
internal interface ICacheOrchestratorContext<TKey, TInner>
    where TKey : notnull
    where TInner : notnull
{
    /// <summary>
    /// Registers or replaces the inner observable associated with <paramref name="key"/>. If a prior
    /// subscription exists for this key, it is disposed and replaced. The supplied observable is
    /// automatically routed through the shared delivery queue and participates in completion
    /// accounting (the downstream stream stays alive until every tracked subscription terminates).
    /// </summary>
    /// <param name="key">The source changeset key whose inner subscription should be (re)registered.</param>
    /// <param name="observable">The new inner observable.</param>
    void Track(TKey key, IObservable<TInner> observable);

    /// <summary>
    /// Removes and disposes the inner subscription associated with <paramref name="key"/>, if any.
    /// No-op if no subscription is currently registered for the key.
    /// </summary>
    /// <param name="key">The source changeset key whose inner subscription should be removed.</param>
    void Untrack(TKey key);

    /// <summary>
    /// Wraps <paramref name="observable"/> with the shared queue's synchronization gate so that
    /// downstream operators (side-effecting <c>Do</c> calls, time-based buffering, etc.) run under
    /// the same serialization as source and inner notifications.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Track"/>, a Serialize-wrapped subscription does NOT participate in
    /// completion accounting: downstream can complete while it is still active.
    /// </remarks>
    /// <typeparam name="T">Value type of the observable being serialized.</typeparam>
    /// <param name="observable">The observable to wrap.</param>
    /// <returns>A new observable whose <c>OnNext</c> delivery runs under the orchestrator's queue lock.</returns>
    IObservable<T> Serialize<T>(IObservable<T> observable);
}
