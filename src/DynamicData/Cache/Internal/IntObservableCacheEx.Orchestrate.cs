// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Internal companion to <see cref="ObservableCacheEx"/>. Hosts extension methods that are part of the
/// internal library surface (operator primitives, composition helpers, lambda adapters) but are not
/// suitable for public exposure. Split across multiple files by concern.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Choosing an Orchestrate* overload (decision table):</strong>
/// </para>
/// <list type="table">
///   <listheader><term>Operator shape</term><description>Use</description></listheader>
///   <item>
///     <term>Single value type (TResult), needs explicit orchestrator class</term>
///     <description><see cref="Orchestrate{TSource, TKey, TInner, TResult, TOrch}"/> + custom <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/> (or subclass <see cref="CacheOrchestratorBase{TSource, TKey, TInner, TResult}"/>).</description>
///   </item>
///   <item>
///     <term>Stateless, simple per-reason logic, value output</term>
///     <description><see cref="Orchestrate{TSource, TKey, TInner, TResult}(IObservable{IChangeSet{TSource, TKey}}, Action{IChangeSet{TSource, TKey}, ICacheOrchestratorContext{TKey, TInner}}, Action{TInner, TKey, IObserver{TResult}}, Action{IObserver{TResult}}?)"/> lambda overload.</description>
///   </item>
///   <item>
///     <term>Output is a cache changeset, you mutate a ChangeAwareCache per source/inner event</term>
///     <description><see cref="OrchestrateChangeSets{TSource, TKey, TInner, TOutput}"/>.</description>
///   </item>
///   <item>
///     <term>Output is a merged cache changeset (inner observables themselves emit cache changesets)</term>
///     <description><see cref="OrchestrateManyChangeSets{TSource, TKey, TDest, TDestKey}"/> (cache overload).</description>
///   </item>
///   <item>
///     <term>Output is a merged list changeset (inner observables emit list changesets)</term>
///     <description><see cref="OrchestrateManyChangeSets{TSource, TKey, TDest}"/> (list overload).</description>
///   </item>
/// </list>
/// </remarks>
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
    /// <typeparam name="TOrch">Concrete orchestrator type returned by the factory. Generic-typed so dispatch sites devirtualize.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="factory">Builds the per-subscription orchestrator from its runtime context and emitter.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> Orchestrate<TSource, TKey, TInner, TResult, TOrch>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<ICacheOrchestratorContext<TKey, TInner>, IObserver<TResult>, TOrch> factory)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
        where TOrch : ICacheOrchestrator<TSource, TKey, TInner, TResult> =>
        new CacheOrchestration<TSource, TKey, TInner, TResult, TOrch>(source, factory).Run();

    /// <summary>
    /// Convenience overload of <see cref="Orchestrate{TSource, TKey, TInner, TResult, TOrch}"/>
    /// for stateless orchestrators or those with state small enough to capture in closures. The
    /// source-change lambda receives the runtime context (for <c>Track</c>/<c>Untrack</c>/
    /// <c>Serialize</c>); the inner lambda receives the downstream emitter directly.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="onSourceChangeSet">Invoked for each source changeset with the runtime context.</param>
    /// <param name="onInner">Invoked for each value emitted by a tracked inner observable, with its key and the downstream emitter.</param>
    /// <param name="onDrainComplete">Optional. Invoked once per drain cycle to flush aggregated state to the emitter. Defaults to a no-op.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> Orchestrate<TSource, TKey, TInner, TResult>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Action<IChangeSet<TSource, TKey>, ICacheOrchestratorContext<TKey, TInner>> onSourceChangeSet,
            Action<TInner, TKey, IObserver<TResult>> onInner,
            Action<IObserver<TResult>>? onDrainComplete = null)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        source.Orchestrate<TSource, TKey, TInner, TResult, LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>>(
            (context, emitter) => new LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(context, emitter, onSourceChangeSet, onInner, onDrainComplete));

    internal sealed class LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(
            ICacheOrchestratorContext<TKey, TInner> context,
            IObserver<TResult> emitter,
            Action<IChangeSet<TSource, TKey>, ICacheOrchestratorContext<TKey, TInner>> onSourceChangeSet,
            Action<TInner, TKey, IObserver<TResult>> onInner,
            Action<IObserver<TResult>>? onDrainComplete)
        : ICacheOrchestrator<TSource, TKey, TInner, TResult>
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
    {
        public void OnSourceChangeSet(IChangeSet<TSource, TKey> changes) => onSourceChangeSet(changes, context);

        public void OnInner(TInner value, TKey key) => onInner(value, key, emitter);

        public void OnDrainComplete(bool isFinal, bool wasReentrant) => onDrainComplete?.Invoke(emitter);
    }
}
