// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Alternate version of MergeManyCacheChangeSets that uses a Comparer of the source, not the destination type
/// So that items from the most important source go into the resulting changeset.
/// </summary>
internal sealed class MergeManyCacheChangeSetsSourceCompare<TObject, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IComparer<TObject> parentCompare, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? childCompare, bool reevalOnRefresh = false)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<IChangeSet<ParentChildEntry, TDestinationKey>>> _changeSetSelector = (obj, key) => selector(obj, key).Transform(dest => new ParentChildEntry(obj, dest));

    private readonly IComparer<ParentChildEntry> _comparer = (childCompare is null) ? new ParentOnlyCompare(parentCompare) : new ParentChildCompare(parentCompare, childCompare);

    private readonly IEqualityComparer<ParentChildEntry>? _equalityComparer = (equalityComparer != null) ? new ParentChildEqualityCompare(equalityComparer) : null;

    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<ParentChildEntry, TDestinationKey>>(
        observer =>
        {
            var locker = new object();
            var cache = new Cache<ChangeSetCache<ParentChildEntry, TDestinationKey>, TKey>();

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<ParentChildEntry, TDestinationKey>(() => cache.Items, _comparer, _equalityComparer);

            // Transform to an cache changeset of child caches of ParentChildEntry, synchronize, update the local copy, and publish.
            var shared = source
                .Transform((obj, key) => new ChangeSetCache<ParentChildEntry, TDestinationKey>(_changeSetSelector(obj, key).Synchronize(locker)))
                .Synchronize(locker)
                .Do(cache.Clone)
                .Publish();

            // Merge the child changeset changes together and apply to the tracker
            var subMergeMany = shared
                .MergeMany(changeSetCache => changeSetCache.Source)
                .Subscribe(
                    changes => changeTracker.ProcessChangeSet(changes, observer),
                    observer.OnError,
                    observer.OnCompleted);

            // When a source item is removed, all of its sub-items need to be removed
            var subRemove = shared
                .OnItemRemoved(cacheChangeSet => changeTracker.RemoveItems(cacheChangeSet.Cache.KeyValues, observer), invokeOnUnsubscribe: false)
                .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues, observer))
                .Subscribe();

            // Optionally attach a handler for Refresh events
            var subRefresh = reevalOnRefresh
                ? shared.OnItemRefreshed(cacheChangeSet => changeTracker.RefreshItems(cacheChangeSet.Cache.Keys, observer)).Subscribe()
                : Disposable.Empty;

            return new CompositeDisposable(shared.Connect(), subMergeMany, subRemove, subRefresh);
        }).Select(changes => changes.Transform(entry => entry.Child));

    private sealed class ParentChildEntry(TObject parent, TDestination child)
    {
        public TObject Parent { get; } = parent;

        public TDestination Child { get; } = child;
    }

    private sealed class ParentChildCompare(IComparer<TObject> comparerParent, IComparer<TDestination> comparerChild) : Comparer<ParentChildEntry>
    {
        public override int Compare(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => comparerParent.Compare(x.Parent, y.Parent) switch
                                    {
                                        0 => comparerChild.Compare(x.Child, y.Child),
                                        int i => i,
                                    },
            (null, null) => 0,
            (null, not null) => 1,
            (not null, null) => -1,
        };
    }

    private sealed class ParentOnlyCompare(IComparer<TObject> comparer) : Comparer<ParentChildEntry>
    {
        public override int Compare(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => comparer.Compare(x.Parent, y.Parent),
            (null, null) => 0,
            (null, not null) => 1,
            (not null, null) => -1,
        };
    }

    private sealed class ParentChildEqualityCompare(IEqualityComparer<TDestination> comparer) : EqualityComparer<ParentChildEntry>
    {
        public override bool Equals(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => comparer.Equals(x.Child, y.Child),
            (null, null) => true,
            _ => false,
        };

        public override int GetHashCode(ParentChildEntry obj) => comparer.GetHashCode(obj.Child);
    }
}
