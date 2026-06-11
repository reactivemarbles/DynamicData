// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Orchestrator contract consumed by the <c>OrchestrateMany</c> primitive. Implementations hold
/// per-subscription state as fields and receive their
/// <see cref="ICacheOrchestratorContext{TKey, TInner}"/> and downstream emitter as constructor
/// arguments supplied by the factory passed to <c>OrchestrateMany</c>. A new orchestrator instance
/// is constructed per subscription, so all state is naturally isolated.
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
    /// <param name="isFinal">
    /// <see langword="true"/> when this is the last drain before the downstream observer receives
    /// <c>OnCompleted</c> (the source changeset and every tracked inner observable have all
    /// completed). Implementations holding deferred state (timer-armed buffers, debounced batches)
    /// should flush synchronously when this is <see langword="true"/>.
    /// </param>
    /// <param name="wasReentrant">
    /// <see langword="true"/> when a reentrant drain occurred during the prior delivery cycle (an
    /// orchestrator emit triggered same-thread re-entry into the queue's drain loop). Most
    /// orchestrators can ignore this; it is exposed for advanced consumers that want to differentiate
    /// the "I just emitted" path from a clean drain cycle.
    /// </param>
    void OnDrainComplete(bool isFinal, bool wasReentrant);
}
