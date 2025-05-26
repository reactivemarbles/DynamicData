// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer => new Subscription(source, selector, observer, equalityComparer, comparer));

    // Maintains state for a single subscription
    private sealed class Subscription : ParentSubscription<ChangeSetCache<TDestination, TDestinationKey>, TKey, IChangeSet<TDestination, TDestinationKey>, IChangeSet<TDestination, TDestinationKey>>
    {
        private readonly Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey> _cache = new();
        private readonly ChangeSetMergeTracker<TDestination, TDestinationKey> _changeSetMergeTracker;
        private readonly Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> _transform;

        public Subscription(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> transform,
            IObserver<IChangeSet<TDestination, TDestinationKey>> observer,
            IEqualityComparer<TDestination>? equalityComparer,
            IComparer<TDestination>? comparer)
            : base(observer)
        {
            _transform = transform;
            _changeSetMergeTracker = new(() => _cache.Items, comparer, equalityComparer);

            CreateParentSubscription(source.Transform((obj, key) => new ChangeSetCache<TDestination, TDestinationKey>(_transform(obj, key))));
        }

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

        protected override void ChildOnNext(IChangeSet<TDestination, TDestinationKey> changes, TKey parentKey) =>
            _changeSetMergeTracker.ProcessChangeSet(changes, null);

        protected override void EmitChanges(IObserver<IChangeSet<TDestination, TDestinationKey>> observer) =>
            _changeSetMergeTracker.EmitChanges(observer);
    }
}
