// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Internal;

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

    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() =>
        Observable.Create<IChangeSet<ParentChildEntry, TDestinationKey>>(observer => new Subscription(source, _changeSetSelector, observer, _comparer, _equalityComparer, reevalOnRefresh))
            .TransformImmutable(entry => entry.Child);

    // Maintains state for a single subscription
    private sealed class Subscription : ParentSubscription<ChangeSetCache<ParentChildEntry, TDestinationKey>, TKey, IChangeSet<ParentChildEntry, TDestinationKey>, IChangeSet<ParentChildEntry, TDestinationKey>>
    {
        private readonly Cache<ChangeSetCache<ParentChildEntry, TDestinationKey>, TKey> _cache = new();
        private readonly ChangeSetMergeTracker<ParentChildEntry, TDestinationKey> _changeSetMergeTracker;
        private readonly Func<TObject, TKey, IObservable<IChangeSet<ParentChildEntry, TDestinationKey>>> _changeSetSelector;
        private readonly bool _reevalOnRefresh;

        public Subscription(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<IChangeSet<ParentChildEntry, TDestinationKey>>> changeSetSelector,
            IObserver<IChangeSet<ParentChildEntry, TDestinationKey>> observer,
            IComparer<ParentChildEntry> comparer,
            IEqualityComparer<ParentChildEntry>? equalityComparer,
            bool reevalOnRefresh)
            : base(observer)
        {
            _changeSetSelector = changeSetSelector;
            _changeSetMergeTracker = new(() => _cache.Items, comparer, equalityComparer);
            _reevalOnRefresh = reevalOnRefresh;

            CreateParentSubscription(source.Transform((obj, key) => new ChangeSetCache<ParentChildEntry, TDestinationKey>(_changeSetSelector(obj, key))));
        }

        protected override void ParentOnNext(IChangeSet<ChangeSetCache<ParentChildEntry, TDestinationKey>, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the cache and emit the changes
                    case ChangeReason.Add or ChangeReason.Update:
                        _cache.AddOrUpdate(change.Current, change.Key);
                        AddChildSubscription(change.Current.Source, change.Key);
                        if (change.Previous.HasValue)
                        {
                            _changeSetMergeTracker.RemoveItems(change.Previous.Value.Cache.KeyValues);
                        }
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        _cache.Remove(change.Key);
                        RemoveChildSubscription(change.Key);
                        _changeSetMergeTracker.RemoveItems(change.Current.Cache.KeyValues);
                        break;

                    case ChangeReason.Refresh:
                        if (_reevalOnRefresh)
                        {
                            _changeSetMergeTracker.RefreshItems(change.Current.Cache.Keys);
                        }
                        break;
                }
            }
        }

        protected override void ChildOnNext(IChangeSet<ParentChildEntry, TDestinationKey> changes, TKey parentKey) =>
            _changeSetMergeTracker.ProcessChangeSet(changes, null);

        protected override void EmitChanges(IObserver<IChangeSet<ParentChildEntry, TDestinationKey>> observer) =>
            _changeSetMergeTracker.EmitChanges(observer);
    }

    private sealed record ParentChildEntry(TObject Parent, TDestination Child);

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
