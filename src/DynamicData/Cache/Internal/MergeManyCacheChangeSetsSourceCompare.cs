// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Alternate version of MergeManyCacheChangeSets that uses a Comparer of the source, not the destination type
/// So that items from the most important source go into the resulting changeset.
/// </summary>
internal sealed class MergeManyCacheChangeSetsSourceCompare<TObject, TKey, TDestination, TDestinationKey>
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    private readonly Func<TObject, TKey, IObservable<IChangeSet<ParentChildEntry, TDestinationKey>>> _changeSetSelector;

    private readonly IComparer<ParentChildEntry>? _comparer;

    private readonly IEqualityComparer<ParentChildEntry>? _equalityComparer;

    private readonly bool _reevalOnRefresh;

    public MergeManyCacheChangeSetsSourceCompare(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IComparer<TObject> parentCompare, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? childCompare, bool reevalOnRefresh = false)
    {
        _source = source;
        _changeSetSelector = (obj, key) => selector(obj, key).Transform(dest => new ParentChildEntry(obj, dest));
        _comparer = (childCompare is null) ? new ParentOnlyCompare(parentCompare) : new ParentChildCompare(parentCompare, childCompare);
        _equalityComparer = (equalityComparer != null) ? new ParentChildEqualityCompare(equalityComparer) : null;
        _reevalOnRefresh = reevalOnRefresh;
    }

    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run()
    {
        return Observable.Create<IChangeSet<ParentChildEntry, TDestinationKey>>(
            observer =>
            {
                var locker = new object();

                // Transform to an observable cache of merge containers.
                var sourceCacheOfCaches = _source
                                            .Transform((obj, key) => new ChangeSetCache<ParentChildEntry, TDestinationKey>(_changeSetSelector(obj, key)))
                                            .Synchronize(locker)
                                            .AsObservableCache();

                var shared = sourceCacheOfCaches.Connect().Publish();

                // this is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<ParentChildEntry, TDestinationKey>(() => sourceCacheOfCaches.Items, _comparer, _equalityComparer);

                // merge the items back together
                var allChanges = shared.MergeMany(mc => mc.Source)
                                                 .Synchronize(locker)
                                                 .Subscribe(
                                                        changes => changeTracker.ProcessChangeSet(changes, observer),
                                                        observer.OnError,
                                                        observer.OnCompleted);

                // when a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .OnItemRemoved(mc => changeTracker.RemoveItems(mc.Cache.KeyValues, observer))
                    .OnItemUpdated((_, prev) => changeTracker.RemoveItems(prev.Cache.KeyValues, observer))
                    .Subscribe();

                // If requested, when the source sees a refresh event, re-evaluate all the keys associated with that source because the priority may have changed
                // Because the comparison is based on the parent, which has just been refreshed.
                var refreshItems = _reevalOnRefresh
                    ? shared.OnItemRefreshed(mc => changeTracker.RefreshItems(mc.Cache.Keys, observer)).Subscribe()
                    : Disposable.Empty;

                return new CompositeDisposable(sourceCacheOfCaches, allChanges, removedItems, refreshItems, shared.Connect());
            }).Transform(entry => entry.Child);
    }

    private sealed class ParentChildEntry
    {
        public ParentChildEntry(TObject parent, TDestination child)
        {
            Parent = parent;
            Child = child;
        }

        public TObject Parent { get; }

        public TDestination Child { get; }
    }

    private sealed class ParentChildCompare : Comparer<ParentChildEntry>
    {
        private readonly IComparer<TObject> _comparerParent;
        private readonly IComparer<TDestination> _comparerChild;

        public ParentChildCompare(IComparer<TObject> comparerParent, IComparer<TDestination> comparerChild)
        {
            _comparerParent = comparerParent;
            _comparerChild = comparerChild;
        }

        public override int Compare(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => _comparerParent.Compare(x.Parent, y.Parent) switch
                                    {
                                        0 => _comparerChild.Compare(x.Child, x.Child),
                                        int i => i,
                                    },
            (null, null) => 0,
            (null, not null) => 1,
            (not null, null) => -1,
        };
    }

    private sealed class ParentOnlyCompare : Comparer<ParentChildEntry>
    {
        private readonly IComparer<TObject> _comparerParent;

        public ParentOnlyCompare(IComparer<TObject> comparer) => _comparerParent = comparer;

        public override int Compare(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => _comparerParent.Compare(x.Parent, x.Parent),
            (null, null) => 0,
            (null, not null) => 1,
            (not null, null) => -1,
        };
    }

    private sealed class ParentChildEqualityCompare : EqualityComparer<ParentChildEntry>
    {
        private readonly IEqualityComparer<TDestination> _comparer;

        public ParentChildEqualityCompare(IEqualityComparer<TDestination> comparer) => _comparer = comparer;

        public override bool Equals(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => _comparer.Equals(x.Child, y.Child),
            (null, null) => true,
            _ => false,
        };

        public override int GetHashCode(ParentChildEntry obj) => _comparer.GetHashCode(obj.Child);
    }
}
