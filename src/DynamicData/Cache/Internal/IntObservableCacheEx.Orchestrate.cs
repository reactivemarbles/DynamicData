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
///     <term>Needs an explicit orchestrator class</term>
///     <description><see cref="OrchestrateSubscriptions{TSource, TKey, TInner, TResult}(IObservable{IChangeSet{TSource, TKey}}, Func{CacheOrchestration{TSource, TKey, TInner, TResult}, IObserver{TResult}, CacheOrchestrator{TSource, TKey, TInner, TResult}})"/> with a subclass of <see cref="CacheOrchestrator{TSource, TKey, TInner, TResult}"/>.</description>
///   </item>
///   <item>
///     <term>Stateless, simple per-reason logic, value output</term>
///     <description>The lambda overload of <c>OrchestrateSubscriptions</c>.</description>
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
    /// with that subscription's <see cref="CacheOrchestration{TSource, TKey, TInner, TResult}"/> and the
    /// downstream observer; the orchestrator instance it returns owns its per-subscription state for the
    /// lifetime of the subscription.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="factory">Builds the per-subscription orchestrator from its orchestration and the downstream observer.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> OrchestrateSubscriptions<TSource, TKey, TInner, TResult>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<CacheOrchestration<TSource, TKey, TInner, TResult>, IObserver<TResult>, CacheOrchestrator<TSource, TKey, TInner, TResult>> factory)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        Observable.Create<TResult>(observer => new CacheOrchestration<TSource, TKey, TInner, TResult>(source, observer, factory));

    /// <summary>
    /// Convenience overload for orchestrators that are stateless, or whose state is small enough to
    /// capture in closures. The source lambda receives the orchestration (for <c>Track</c>,
    /// <c>Untrack</c> and <c>Serialize</c>); the inner lambda receives the downstream observer directly.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="onSourceNext">Invoked for each source changeset with the orchestration.</param>
    /// <param name="onItemSourceNext">Invoked for each value emitted by a tracked inner observable, with its key and the downstream observer.</param>
    /// <param name="onFrameEnded">Optional. Invoked at the end of each delivery frame to emit accumulated state. Defaults to a no-op.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> OrchestrateSubscriptions<TSource, TKey, TInner, TResult>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Action<IChangeSet<TSource, TKey>, CacheOrchestration<TSource, TKey, TInner, TResult>> onSourceNext,
            Action<TInner, TKey, IObserver<TResult>> onItemSourceNext,
            Action<IObserver<TResult>>? onFrameEnded = null)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        source.OrchestrateSubscriptions<TSource, TKey, TInner, TResult>(
            (context, emitter) => new LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(context, emitter, onSourceNext, onItemSourceNext, onFrameEnded));

    internal sealed class LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(
            CacheOrchestration<TSource, TKey, TInner, TResult> context,
            IObserver<TResult> emitter,
            Action<IChangeSet<TSource, TKey>, CacheOrchestration<TSource, TKey, TInner, TResult>> onSourceNext,
            Action<TInner, TKey, IObserver<TResult>> onItemSourceNext,
            Action<IObserver<TResult>>? onFrameEnded)
        : CacheOrchestrator<TSource, TKey, TInner, TResult>(context, emitter)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
    {
        public override void OnSourceNext(IChangeSet<TSource, TKey> changes) => onSourceNext(changes, Context);

        public override void OnItemSourceNext(TInner value, TKey key) => onItemSourceNext(value, key, Emitter);

        public override void OnFrameEnded(bool isFinal) => onFrameEnded?.Invoke(Emitter);
    }
}
