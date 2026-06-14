// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Contract for orchestrators driven by <c>Orchestrate</c>. A fresh instance is constructed
/// per subscription (via the factory passed to <c>Orchestrate</c>), so per-subscription state
/// can live as fields with no isolation concerns.
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
    /// <see langword="true"/> when a reentrant drain occurred during the prior delivery cycle
    /// (an orchestrator emit triggered same-thread re-entry into the drain loop). Most
    /// orchestrators ignore this.
    /// </param>
    void OnDrainComplete(bool isFinal, bool wasReentrant);
}
