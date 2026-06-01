// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.List.Internal;

namespace DynamicData.Cache.Internal;

internal static partial class IntObservableCacheEx
{
    /// <summary>
    /// Orchestrates per-source-key inner observables that themselves emit list changesets, merging
    /// the live set of all such inner streams into a single output list changeset. Specialization of
    /// <see cref="OrchestrateMany{TSource, TKey, TInner, TResult}(IObservable{IChangeSet{TSource, TKey}}, ICacheOrchestrator{TSource, TKey, TInner, TResult})"/>
    /// for the cache-source-to-list-merged shape used by MergeManyListChangeSets in Cache/Internal/.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key.</typeparam>
    /// <typeparam name="TDest">Type of items in the output (merged) list changeset.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="changeSetSelector">Builds the per-source-key child list changeset stream from the source item and its key.</param>
    /// <param name="equalityComparer">Optional equality comparer used when storing per-source-key snapshots of inner contents.</param>
    /// <returns>An observable list changeset representing the merged union of all live inner changesets.</returns>
    public static IObservable<IChangeSet<TDest>> OrchestrateManyMergedList<TSource, TKey, TDest>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TKey, IObservable<IChangeSet<TDest>>> changeSetSelector,
            IEqualityComparer<TDest>? equalityComparer = null)
        where TSource : notnull
        where TKey : notnull
        where TDest : notnull =>
        Observable.Create<IChangeSet<TDest>>(observer =>
            source.OrchestrateMany(new MergedListOrchestrator<TSource, TKey, TDest>(changeSetSelector, equalityComparer))
                  .SubscribeSafe(observer));

    private sealed class MergedListOrchestrator<TSource, TKey, TDest> : ICacheOrchestrator<TSource, TKey, IChangeSet<TDest>, IChangeSet<TDest>>
        where TSource : notnull
        where TKey : notnull
        where TDest : notnull
    {
        private readonly Dictionary<TKey, ClonedListChangeSet<TDest>> _entries = new();
        private readonly ChangeSetMergeTracker<TDest> _tracker = new();
        private readonly Func<TSource, TKey, IObservable<IChangeSet<TDest>>> _changeSetSelector;
        private readonly IEqualityComparer<TDest>? _equalityComparer;
        private ICacheOrchestratorContext<TKey, IChangeSet<TDest>> _context = null!;

        public MergedListOrchestrator(
                Func<TSource, TKey, IObservable<IChangeSet<TDest>>> changeSetSelector,
                IEqualityComparer<TDest>? equalityComparer)
        {
            _changeSetSelector = changeSetSelector;
            _equalityComparer = equalityComparer;
        }

        public void Initialize(ICacheOrchestratorContext<TKey, IChangeSet<TDest>> context) => _context = context;

        public void OnSourceChangeSet(IChangeSet<TSource, TKey> changes)
        {
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add or ChangeReason.Update:
                        var entry = new ClonedListChangeSet<TDest>(_context.Serialize(_changeSetSelector(change.Current, change.Key).RemoveIndex()), _equalityComparer);
                        if (_entries.TryGetValue(change.Key, out var previous))
                        {
                            _entries.Remove(change.Key);
                            _tracker.RemoveItems(previous.List);
                        }

                        _entries[change.Key] = entry;
                        _context.Track(change.Key, entry.Source);
                        break;

                    case ChangeReason.Remove:
                        if (_entries.TryGetValue(change.Key, out var removed))
                        {
                            _entries.Remove(change.Key);
                            _tracker.RemoveItems(removed.List);
                        }

                        _context.Track(change.Key, null);
                        break;
                }
            }
        }

        public void OnInner(IChangeSet<TDest> child, TKey parentKey) => _tracker.ProcessChangeSet(child, null);

        public void Emit(IObserver<IChangeSet<TDest>> observer) => _tracker.EmitChanges(observer);
    }
}
