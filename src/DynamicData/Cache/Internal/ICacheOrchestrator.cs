// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Orchestrator contract consumed by the <c>OrchestrateMany</c> primitive. Implementations hold
/// per-subscription state as fields and receive their
/// <see cref="ICacheOrchestratorContext{TKey, TInner}"/> and downstream emitter once via
/// <see cref="Initialize"/> before any other method is called.
/// </summary>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream via the emitter.</typeparam>
internal interface ICacheOrchestrator<TSource, TKey, TInner, TResult>
    where TSource : notnull
    where TKey : notnull
    where TInner : notnull
{
    /// <summary>
    /// Invoked exactly once by <c>OrchestrateMany</c> before subscribing to the source. Implementations
    /// should capture <paramref name="context"/> and <paramref name="emitter"/> in private fields for
    /// use by subsequent method calls. The supplied <paramref name="emitter"/> is a sub-queue of the
    /// orchestrator's shared delivery queue: every <c>OnNext</c>/<c>OnError</c>/<c>OnCompleted</c> on
    /// it is automatically serialized with source and inner notifications.
    /// </summary>
    /// <param name="context">The runtime context, scoped to this subscription's lifetime.</param>
    /// <param name="emitter">The downstream observer, fronted by a serializing sub-queue.</param>
    void Initialize(ICacheOrchestratorContext<TKey, TInner> context, IObserver<TResult> emitter);

    /// <summary>
    /// Invoked for each source changeset.
    /// </summary>
    /// <param name="changes">The source changeset.</param>
    void OnSourceChangeSet(IChangeSet<TSource, TKey> changes);

    /// <summary>
    /// Invoked for each value emitted by a tracked inner observable, paired with its source key.
    /// </summary>
    /// <param name="value">The value emitted by the inner observable.</param>
    /// <param name="key">The source key the inner observable was registered against.</param>
    void OnInner(TInner value, TKey key);

    /// <summary>
    /// Invoked at the end of each drain cycle of the shared delivery queue. Implementations that
    /// coalesce source and inner activity into per-drain emissions flush their accumulated state to
    /// the emitter here. May be invoked multiple times per source event when the orchestrator's own
    /// emit triggers a reentrant drain; subsequent calls are no-ops when there is nothing to flush.
    /// </summary>
    void OnDrainComplete();
}
