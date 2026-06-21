// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="changeSetSelector">The changeSetSelector value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
/// <param name="comparer">The comparer value.</param>
internal sealed class MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer => new Subscription(source, changeSetSelector, observer, equalityComparer, comparer));
    // Maintains state for a single subscription

/// <summary>
/// Provides members for the Subscription class.
/// </summary>
private sealed class Subscription : CacheParentSubscription<ChangeSetCache<TDestination, TDestinationKey>, TKey, IChangeSet<TDestination, TDestinationKey>, IChangeSet<TDestination, TDestinationKey>>
    {
        /// <summary>
        /// The _cache field.
        /// </summary>
        private readonly Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey> _cache = new();

        /// <summary>
        /// The _changeSetMergeTracker field.
        /// </summary>
        private readonly ChangeSetMergeTracker<TDestination, TDestinationKey> _changeSetMergeTracker;

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="changeSetSelector">The changeSetSelector value.</param>
        /// <param name="observer">The observer value.</param>
        /// <param name="equalityComparer">The equalityComparer value.</param>
        /// <param name="comparer">The comparer value.</param>
        public Subscription(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector,
            IObserver<IChangeSet<TDestination, TDestinationKey>> observer,
            IEqualityComparer<TDestination>? equalityComparer,
            IComparer<TDestination>? comparer)
            : base(observer)
        {
            _changeSetMergeTracker = new(() => _cache.Items, comparer, equalityComparer);

            // Child Observable has to go into the ChangeSetCache so the locking protects it
            CreateParentSubscription(source.Transform((obj, key) =>
                new ChangeSetCache<TDestination, TDestinationKey>(MakeChildObservable(changeSetSelector(obj, key).IgnoreSameReferenceUpdate()))));
        }

        /// <summary>
        /// Executes the ParentOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        protected override void ParentOnNext(IChangeSet<ChangeSetCache<TDestination, TDestinationKey>, TKey> changes)
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
                }
            }
        }

        /// <summary>
        /// Executes the ChildOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        /// <param name="parentKey">The parentKey value.</param>
        protected override void ChildOnNext(IChangeSet<TDestination, TDestinationKey> changes, TKey parentKey) =>
            _changeSetMergeTracker.ProcessChangeSet(changes, null);

        /// <summary>
        /// Executes the EmitChanges operation.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        protected override void EmitChanges(IObserver<IChangeSet<TDestination, TDestinationKey>> observer) =>
            _changeSetMergeTracker.EmitChanges(observer);
    }
}
