// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Alternate version of MergeManyCacheChangeSets that uses a Comparer of the source, not the destination type
/// So that items from the most important source go into the resulting changeset.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="selector">The selector value.</param>
/// <param name="parentCompare">The parentCompare value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
/// <param name="childCompare">The childCompare value.</param>
/// <param name="reevalOnRefresh">The reevalOnRefresh value.</param>
internal sealed class MergeManyCacheChangeSetsSourceCompare<TObject, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IComparer<TObject> parentCompare, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? childCompare, bool reevalOnRefresh = false)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    /// <summary>
    /// The _changeSetSelector field.
    /// </summary>
    private readonly Func<TObject, TKey, IObservable<IChangeSet<ParentChildEntry, TDestinationKey>>> _changeSetSelector = (obj, key) => selector(obj, key).Transform(dest => new ParentChildEntry(obj, dest));

    /// <summary>
    /// The _comparer field.
    /// </summary>
    private readonly IComparer<ParentChildEntry> _comparer = (childCompare is null) ? new ParentOnlyCompare(parentCompare) : new ParentChildCompare(parentCompare, childCompare);

    /// <summary>
    /// The _equalityComparer field.
    /// </summary>
    private readonly IEqualityComparer<ParentChildEntry>? _equalityComparer = (equalityComparer != null) ? new ParentChildEqualityCompare(equalityComparer) : null;

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() =>
        Observable.Create<IChangeSet<ParentChildEntry, TDestinationKey>>(observer => new Subscription(source, _changeSetSelector, observer, _comparer, _equalityComparer, reevalOnRefresh))
            .TransformImmutable(entry => entry.Child);
    // Maintains state for a single subscription

/// <summary>
/// Provides members for the Subscription class.
/// </summary>
private sealed class Subscription : CacheParentSubscription<ChangeSetCache<ParentChildEntry, TDestinationKey>, TKey, IChangeSet<ParentChildEntry, TDestinationKey>, IChangeSet<ParentChildEntry, TDestinationKey>>
    {
        /// <summary>
        /// The _cache field.
        /// </summary>
        private readonly Cache<ChangeSetCache<ParentChildEntry, TDestinationKey>, TKey> _cache = new();

        /// <summary>
        /// The _changeSetMergeTracker field.
        /// </summary>
        private readonly ChangeSetMergeTracker<ParentChildEntry, TDestinationKey> _changeSetMergeTracker;

        /// <summary>
        /// The _reevalOnRefresh field.
        /// </summary>
        private readonly bool _reevalOnRefresh;

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="changeSetSelector">The changeSetSelector value.</param>
        /// <param name="observer">The observer value.</param>
        /// <param name="comparer">The comparer value.</param>
        /// <param name="equalityComparer">The equalityComparer value.</param>
        /// <param name="reevalOnRefresh">The reevalOnRefresh value.</param>
        public Subscription(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<IChangeSet<ParentChildEntry, TDestinationKey>>> changeSetSelector,
            IObserver<IChangeSet<ParentChildEntry, TDestinationKey>> observer,
            IComparer<ParentChildEntry> comparer,
            IEqualityComparer<ParentChildEntry>? equalityComparer,
            bool reevalOnRefresh)
            : base(observer)
        {
            _changeSetMergeTracker = new(() => _cache.Items, comparer, equalityComparer);
            _reevalOnRefresh = reevalOnRefresh;

            // Child Observable has to go into the ChangeSetCache so the locking protects it
            CreateParentSubscription(source.Transform((obj, key) =>
                new ChangeSetCache<ParentChildEntry, TDestinationKey>(MakeChildObservable(changeSetSelector(obj, key).IgnoreSameReferenceUpdate()))));
        }

        /// <summary>
        /// Executes the ParentOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
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

        /// <summary>
        /// Executes the ChildOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        /// <param name="parentKey">The parentKey value.</param>
        protected override void ChildOnNext(IChangeSet<ParentChildEntry, TDestinationKey> changes, TKey parentKey) =>
            _changeSetMergeTracker.ProcessChangeSet(changes, null);

        /// <summary>
        /// Executes the EmitChanges operation.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        protected override void EmitChanges(IObserver<IChangeSet<ParentChildEntry, TDestinationKey>> observer) =>
            _changeSetMergeTracker.EmitChanges(observer);
    }

    /// <summary>
    /// Represents the ParentChildEntry record.
    /// </summary>
    /// <param name="Parent">The Parent value.</param>
    /// <param name="Child">The Child value.</param>
    private sealed record ParentChildEntry(TObject Parent, TDestination Child);

/// <summary>
/// Provides members for the ParentChildCompare class.
/// </summary>
/// <param name="comparerParent">The comparerParent value.</param>
/// <param name="comparerChild">The comparerChild value.</param>
private sealed class ParentChildCompare(IComparer<TObject> comparerParent, IComparer<TDestination> comparerChild) : Comparer<ParentChildEntry>
    {
        /// <summary>
        /// Executes the Compare operation.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Provides members for the ParentOnlyCompare class.
/// </summary>
/// <param name="comparer">The comparer value.</param>
private sealed class ParentOnlyCompare(IComparer<TObject> comparer) : Comparer<ParentChildEntry>
    {
        /// <summary>
        /// Executes the Compare operation.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <returns>The result of the operation.</returns>
        public override int Compare(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => comparer.Compare(x.Parent, y.Parent),
            (null, null) => 0,
            (null, not null) => 1,
            (not null, null) => -1,
        };
    }

/// <summary>
/// Provides members for the ParentChildEqualityCompare class.
/// </summary>
/// <param name="comparer">The comparer value.</param>
private sealed class ParentChildEqualityCompare(IEqualityComparer<TDestination> comparer) : EqualityComparer<ParentChildEntry>
    {
        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <returns>The result of the operation.</returns>
        public override bool Equals(ParentChildEntry? x, ParentChildEntry? y) => (x, y) switch
        {
            (not null, not null) => comparer.Equals(x.Child, y.Child),
            (null, null) => true,
            _ => false,
        };

        /// <summary>
        /// Executes the GetHashCode operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result of the operation.</returns>
        public override int GetHashCode(ParentChildEntry obj) => comparer.GetHashCode(obj.Child);
    }
}
