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
        Observable.Create<IChangeSet<TDest, TDestKey>>(observer =>
            source.OrchestrateMany(new MergedOrchestrator<TSource, TKey, TDest, TDestKey>(changeSetSelector, equalityComparer, comparer, reevalOnRefresh))
                  .SubscribeSafe(observer));

    private sealed class MergedOrchestrator<TSource, TKey, TDest, TDestKey> : ICacheOrchestrator<TSource, TKey, IChangeSet<TDest, TDestKey>, IChangeSet<TDest, TDestKey>>
        where TSource : notnull
        where TKey : notnull
        where TDest : notnull
        where TDestKey : notnull
    {
        private readonly Cache<ChangeSetCache<TDest, TDestKey>, TKey> _cache = new();
        private readonly ChangeSetMergeTracker<TDest, TDestKey> _tracker;
        private readonly Func<TSource, TKey, IObservable<IChangeSet<TDest, TDestKey>>> _changeSetSelector;
        private readonly bool _reevalOnRefresh;
        private ICacheOrchestratorContext<TKey, IChangeSet<TDest, TDestKey>> _context = null!;

        public MergedOrchestrator(
                Func<TSource, TKey, IObservable<IChangeSet<TDest, TDestKey>>> changeSetSelector,
                IEqualityComparer<TDest>? equalityComparer,
                IComparer<TDest>? comparer,
                bool reevalOnRefresh)
        {
            _changeSetSelector = changeSetSelector;
            _reevalOnRefresh = reevalOnRefresh;
            _tracker = new ChangeSetMergeTracker<TDest, TDestKey>(() => _cache.Items, comparer, equalityComparer);
        }

        public void Initialize(ICacheOrchestratorContext<TKey, IChangeSet<TDest, TDestKey>> context) => _context = context;

        public void OnSourceChangeSet(IChangeSet<TSource, TKey> changes)
        {
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add or ChangeReason.Update:
                        var previous = _cache.Lookup(change.Key);
                        var entry = new ChangeSetCache<TDest, TDestKey>(_context.Serialize(_changeSetSelector(change.Current, change.Key).IgnoreSameReferenceUpdate()));
                        _cache.AddOrUpdate(entry, change.Key);
                        _context.Track(change.Key, entry.Source);
                        if (previous.HasValue)
                        {
                            _tracker.RemoveItems(previous.Value.Cache.KeyValues);
                        }
                        break;

                    case ChangeReason.Remove:
                        if (_cache.Lookup(change.Key) is { HasValue: true } removed)
                        {
                            // Remove from _cache BEFORE telling the tracker, so the tracker's re-evaluation
                            // does not consider this entry's items as candidates for "best value" selection.
                            _cache.Remove(change.Key);
                            _tracker.RemoveItems(removed.Value.Cache.KeyValues);
                        }

                        _context.Track(change.Key, null);
                        break;

                    case ChangeReason.Refresh when _reevalOnRefresh:
                        if (_cache.Lookup(change.Key) is { HasValue: true } current)
                        {
                            _tracker.RefreshItems(current.Value.Cache.Keys);
                        }
                        break;
                }
            }
        }

        public void OnInner(IChangeSet<TDest, TDestKey> child, TKey parentKey) => _tracker.ProcessChangeSet(child, null);

        public void Emit(IObserver<IChangeSet<TDest, TDestKey>> observer) => _tracker.EmitChanges(observer);
    }
}
