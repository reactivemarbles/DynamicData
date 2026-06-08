// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal static partial class IntObservableCacheEx
{
    /// <summary>
    /// Orchestrates per-source-key inner observables that themselves emit cache changesets, merging
    /// the live set of all such inner streams into a single output changeset. Specialization of
    /// <see cref="OrchestrateMany{TSource, TKey, TInner, TResult}(IObservable{IChangeSet{TSource, TKey}}, ICacheOrchestrator{TSource, TKey, TInner, TResult})"/>
    /// for the cache-source-to-cache-merged shape used by MergeManyCacheChangeSets,
    /// MergeManyCacheChangeSetsSourceCompare, and TransformManyAsync.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TDest">Type of items in the output (merged) changeset.</typeparam>
    /// <typeparam name="TDestKey">Type of the output changeset key.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="changeSetSelector">Builds the per-source-key child changeset stream from the source item and its key.</param>
    /// <param name="equalityComparer">Optional equality comparer for the destination items.</param>
    /// <param name="comparer">Optional ordering comparer for the destination items.</param>
    /// <param name="reevalOnRefresh">When <see langword="true"/>, a <c>Refresh</c> on the source forces re-evaluation of the corresponding child entries via the tracker.</param>
    /// <returns>An observable changeset representing the merged union of all live inner changesets.</returns>
    public static IObservable<IChangeSet<TDest, TDestKey>> OrchestrateManyMerged<TSource, TKey, TDest, TDestKey>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TKey, IObservable<IChangeSet<TDest, TDestKey>>> changeSetSelector,
            IEqualityComparer<TDest>? equalityComparer = null,
            IComparer<TDest>? comparer = null,
            bool reevalOnRefresh = false)
        where TSource : notnull
        where TKey : notnull
        where TDest : notnull
        where TDestKey : notnull =>
        source.OrchestrateMany<TSource, TKey, IChangeSet<TDest, TDestKey>, IChangeSet<TDest, TDestKey>>(
            (context, emitter) => new MergedOrchestrator<TSource, TKey, TDest, TDestKey>(context, emitter, changeSetSelector, equalityComparer, comparer, reevalOnRefresh));

    private sealed class MergedOrchestrator<TSource, TKey, TDest, TDestKey> : OrchestratorCacheChangeBase<TSource, TKey, IChangeSet<TDest, TDestKey>, IChangeSet<TDest, TDestKey>>
        where TSource : notnull
        where TKey : notnull
        where TDest : notnull
        where TDestKey : notnull
    {
        private readonly Cache<ChangeSetCache<TDest, TDestKey>, TKey> _cache = new();
        private readonly ChangeSetMergeTracker<TDest, TDestKey> _tracker;
        private readonly Func<TSource, TKey, IObservable<IChangeSet<TDest, TDestKey>>> _changeSetSelector;
        private readonly bool _reevalOnRefresh;

        public MergedOrchestrator(
                ICacheOrchestratorContext<TKey, IChangeSet<TDest, TDestKey>> context,
                IObserver<IChangeSet<TDest, TDestKey>> emitter,
                Func<TSource, TKey, IObservable<IChangeSet<TDest, TDestKey>>> changeSetSelector,
                IEqualityComparer<TDest>? equalityComparer,
                IComparer<TDest>? comparer,
                bool reevalOnRefresh)
            : base(context, emitter)
        {
            _changeSetSelector = changeSetSelector;
            _reevalOnRefresh = reevalOnRefresh;
            _tracker = new ChangeSetMergeTracker<TDest, TDestKey>(() => _cache.Items, comparer, equalityComparer);
        }

        public override void OnInner(IChangeSet<TDest, TDestKey> child, TKey parentKey) => _tracker.ProcessChangeSet(child, null);

        public override void OnDrainComplete(bool sourcesCompleted) => _tracker.EmitChanges(Emitter);

        protected override void OnItemAdded(TSource item, TKey key) => SubscribeChild(item, key);

        protected override void OnItemUpdated(TSource current, TSource previous, TKey key)
        {
            var prior = _cache.Lookup(key);
            SubscribeChild(current, key);
            if (prior.HasValue)
            {
                _tracker.RemoveItems(prior.Value.Cache.KeyValues);
            }
        }

        protected override void OnItemRemoved(TSource item, TKey key)
        {
            if (_cache.Lookup(key) is { HasValue: true } removed)
            {
                // Remove from _cache BEFORE telling the tracker, so the tracker's re-evaluation
                // does not consider this entry's items as candidates for "best value" selection.
                _cache.Remove(key);
                _tracker.RemoveItems(removed.Value.Cache.KeyValues);
            }

            Context.Track(key, null);
        }

        protected override void OnItemRefreshed(TSource item, TKey key)
        {
            if (_reevalOnRefresh && _cache.Lookup(key) is { HasValue: true } current)
            {
                _tracker.RefreshItems(current.Value.Cache.Keys);
            }
        }

        private void SubscribeChild(TSource item, TKey key)
        {
            var entry = new ChangeSetCache<TDest, TDestKey>(Context.Serialize(_changeSetSelector(item, key).IgnoreSameReferenceUpdate()));
            _cache.AddOrUpdate(entry, key);
            Context.Track(key, entry.Source);
        }
    }
}
