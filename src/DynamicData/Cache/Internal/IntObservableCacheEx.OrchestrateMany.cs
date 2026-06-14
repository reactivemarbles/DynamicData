// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Internal companion to <see cref="ObservableCacheEx"/>. Hosts extension methods that are part of the
/// internal library surface (operator primitives, composition helpers, lambda adapters) but are not
/// suitable for public exposure. Split across multiple files by concern.
/// </summary>
internal static partial class IntObservableCacheEx
{
    /// <summary>
    /// Orchestrates a keyed source changeset and a dynamic set of per-key inner observables into a
    /// single result stream. The supplied <paramref name="factory"/> is invoked on every subscription
    /// with the per-subscription <see cref="ICacheOrchestratorContext{TKey, TInner}"/> and downstream
    /// emitter; the orchestrator instance it returns owns its per-subscription state for the lifetime
    /// of the subscription.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream.</typeparam>
    /// <typeparam name="TOrch">
    /// Concrete orchestrator type returned by the factory. Generic over the concrete type so dispatch
    /// sites devirtualize. C# generic inference resolves this from the factory's return type.
    /// </typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="factory">Builds the per-subscription orchestrator from its runtime context and emitter.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> OrchestrateMany<TSource, TKey, TInner, TResult, TOrch>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<ICacheOrchestratorContext<TKey, TInner>, IObserver<TResult>, TOrch> factory)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
        where TOrch : ICacheOrchestrator<TSource, TKey, TInner, TResult> =>
        new Orchestration<TSource, TKey, TInner, TResult, TOrch>(source, factory).Run();

    /// <summary>
    /// Convenience overload of <see cref="OrchestrateMany{TSource, TKey, TInner, TResult, TOrch}"/>
    /// that wraps three lambdas into an <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/>.
    /// The source-change lambda receives the full <see cref="ICacheOrchestratorContext{TKey, TInner}"/>
    /// so it can call <see cref="ICacheOrchestratorContext{TKey, TInner}.Track"/>,
    /// <see cref="ICacheOrchestratorContext{TKey, TInner}.Untrack"/>, or
    /// <see cref="ICacheOrchestratorContext{TKey, TInner}.Serialize"/> as needed.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream by <paramref name="onDrainComplete"/>.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="onSourceChangeSet">Invoked for each source changeset, paired with the runtime context.</param>
    /// <param name="onInner">Invoked for each value emitted by a tracked inner observable, paired with its key.</param>
    /// <param name="onDrainComplete">Invoked once per drain cycle to flush the aggregated state to the emitter.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> OrchestrateMany<TSource, TKey, TInner, TResult>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Action<IChangeSet<TSource, TKey>, ICacheOrchestratorContext<TKey, TInner>> onSourceChangeSet,
            Action<TInner, TKey> onInner,
            Action<IObserver<TResult>> onDrainComplete)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        source.OrchestrateMany<TSource, TKey, TInner, TResult, LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>>(
            (context, emitter) => new LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(context, emitter, onSourceChangeSet, onInner, onDrainComplete));

    internal sealed class LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(
            ICacheOrchestratorContext<TKey, TInner> context,
            IObserver<TResult> emitter,
            Action<IChangeSet<TSource, TKey>, ICacheOrchestratorContext<TKey, TInner>> onSourceChangeSet,
            Action<TInner, TKey> onInner,
            Action<IObserver<TResult>> onDrainComplete)
        : ICacheOrchestrator<TSource, TKey, TInner, TResult>
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
    {
        public void OnSourceChangeSet(IChangeSet<TSource, TKey> changes) => onSourceChangeSet(changes, context);

        public void OnInner(TInner value, TKey key) => onInner(value, key);

        public void OnDrainComplete(bool isFinal, bool wasReentrant) => onDrainComplete(emitter);
    }
}
