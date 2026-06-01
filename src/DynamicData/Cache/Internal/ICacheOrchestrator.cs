// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Orchestrator contract consumed by the <c>OrchestrateMany</c> primitive. Implementations hold
/// per-subscription state as fields and receive their
/// <see cref="ICacheOrchestratorContext{TKey, TInner}"/> once via <see cref="Initialize"/> before
/// any other method is called.
/// </summary>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream by <see cref="Emit"/>.</typeparam>
internal interface ICacheOrchestrator<TSource, TKey, TInner, TResult>
    where TSource : notnull
    where TKey : notnull
    where TInner : notnull
{
    /// <summary>
    /// Invoked exactly once by <c>OrchestrateMany</c> before subscribing to the source. Implementations
    /// should capture <paramref name="context"/> in a private field for use by subsequent method calls.
    /// </summary>
    /// <param name="context">The runtime context, scoped to this subscription's lifetime.</param>
    void Initialize(ICacheOrchestratorContext<TKey, TInner> context);

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
    /// Invoked once per drain cycle of the shared delivery queue to flush aggregated state to
    /// the downstream <paramref name="observer"/>.
    /// </summary>
    /// <param name="observer">The downstream observer.</param>
    void Emit(IObserver<TResult> observer);
}
