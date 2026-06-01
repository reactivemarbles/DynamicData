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
    /// single result stream. The supplied <paramref name="orchestrator"/> owns the per-subscription
    /// state and is wired to an <see cref="ICacheOrchestratorContext{TKey, TInner}"/> via
    /// <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}.Initialize"/>.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="orchestrator">The orchestrator implementation.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> OrchestrateMany<TSource, TKey, TInner, TResult>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            ICacheOrchestrator<TSource, TKey, TInner, TResult> orchestrator)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        new Orchestrator<TSource, TKey, TInner, TResult>(source, orchestrator).Run();

    /// <summary>
    /// Convenience overload that wraps three lambdas into an <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/>
    /// and delegates to <see cref="OrchestrateMany{TSource, TKey, TInner, TResult}(IObservable{IChangeSet{TSource, TKey}}, ICacheOrchestrator{TSource, TKey, TInner, TResult})"/>.
    /// Does not expose <see cref="ICacheOrchestratorContext{TKey, TInner}.Serialize"/> or
    /// <see cref="ICacheOrchestratorContext{TKey, TInner}.ScheduleEmit"/>; operators that need either
    /// must implement <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/> directly.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream by <paramref name="emit"/>.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="onSourceChangeSet">Invoked for each source changeset, paired with a <c>track</c> callback.</param>
    /// <param name="onInner">Invoked for each value emitted by a tracked inner observable, paired with its key.</param>
    /// <param name="emit">Invoked once per drain cycle to flush the aggregated state to the observer.</param>
    /// <returns>An observable that orchestrates source and inner activity into a single result stream.</returns>
    public static IObservable<TResult> OrchestrateMany<TSource, TKey, TInner, TResult>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Action<IChangeSet<TSource, TKey>, Action<TKey, IObservable<TInner>?>> onSourceChangeSet,
            Action<TInner, TKey> onInner,
            Action<IObserver<TResult>> emit)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        source.OrchestrateMany(AsCacheOrchestrator<TSource, TKey, TInner, TResult>(onSourceChangeSet, onInner, emit));

    /// <summary>
    /// Wraps three lambdas into an <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/>.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
    /// <typeparam name="TResult">Type delivered downstream.</typeparam>
    /// <param name="onSourceChangeSet">Invoked for each source changeset, paired with a <c>track</c> callback.</param>
    /// <param name="onInner">Invoked for each value emitted by a tracked inner observable, paired with its key.</param>
    /// <param name="emit">Invoked once per drain cycle to flush the aggregated state to the observer.</param>
    /// <returns>An orchestrator that routes the three callbacks through its <see cref="ICacheOrchestratorContext{TKey, TInner}"/>.</returns>
    public static ICacheOrchestrator<TSource, TKey, TInner, TResult> AsCacheOrchestrator<TSource, TKey, TInner, TResult>(
            Action<IChangeSet<TSource, TKey>, Action<TKey, IObservable<TInner>?>> onSourceChangeSet,
            Action<TInner, TKey> onInner,
            Action<IObserver<TResult>> emit)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull =>
        new LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(onSourceChangeSet, onInner, emit);

    private sealed class LambdaCacheOrchestrator<TSource, TKey, TInner, TResult>(
            Action<IChangeSet<TSource, TKey>, Action<TKey, IObservable<TInner>?>> onSourceChangeSet,
            Action<TInner, TKey> onInner,
            Action<IObserver<TResult>> emit)
        : ICacheOrchestrator<TSource, TKey, TInner, TResult>
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
    {
        private ICacheOrchestratorContext<TKey, TInner> _context = null!;

        public void Initialize(ICacheOrchestratorContext<TKey, TInner> context) => _context = context;

        public void OnSourceChangeSet(IChangeSet<TSource, TKey> changes) => onSourceChangeSet(changes, _context.Track);

        public void OnInner(TInner value, TKey key) => onInner(value, key);

        public void Emit(IObserver<TResult> observer) => emit(observer);
    }
}
