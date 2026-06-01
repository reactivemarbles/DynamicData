// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Runtime context exposed to an <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/>
/// via <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}.Initialize"/>. Provides per-key
/// inner observable lifecycle management, a hook to serialize arbitrary observables through the same
/// shared queue as source and inner notifications, and a primitive to schedule future drain cycles.
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
    /// that any operators chained downstream (e.g. side-effecting <c>Do</c> calls) run under the same
    /// serialization that source and inner notifications already enjoy.
    /// </summary>
    /// <typeparam name="T">Value type of the observable being serialized.</typeparam>
    /// <param name="observable">The observable to wrap.</param>
    /// <returns>A new observable whose <c>OnNext</c> delivery runs under the orchestrator's queue lock.</returns>
    IObservable<T> Serialize<T>(IObservable<T> observable);

    /// <summary>
    /// Schedules a one-shot future drain cycle: after <paramref name="dueTime"/> elapses on
    /// <paramref name="scheduler"/>, a notification enters the shared delivery queue. If
    /// <paramref name="onFire"/> is provided it is invoked synchronously during that drain (before
    /// the drain's <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}.Emit"/> call),
    /// allowing orchestrators to signal state to themselves before flushing aggregated work downstream.
    /// </summary>
    /// <param name="dueTime">The delay before the drain is triggered.</param>
    /// <param name="scheduler">Scheduler used for the timer.</param>
    /// <param name="onFire">Optional callback invoked inside the triggered drain, before <c>Emit</c>.</param>
    /// <returns>A disposable that cancels the pending drain if disposed before <paramref name="dueTime"/>.</returns>
    IDisposable ScheduleEmit(TimeSpan dueTime, IScheduler scheduler, Action? onFire = null);
}
