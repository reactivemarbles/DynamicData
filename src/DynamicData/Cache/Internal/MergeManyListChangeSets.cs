// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Internal;
using DynamicData.List.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles List ChangeSets.
/// </summary>
internal sealed class MergeManyListChangeSets<TObject, TKey, TDestination>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination>>> selector, IEqualityComparer<TDestination>? equalityComparer)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination>> Run() => Observable.Create<IChangeSet<TDestination>>(
        observer => new Subscription(source, selector, observer, equalityComparer));

    // Maintains state for a single subscription
    private sealed class Subscription : CacheParentSubscription<ClonedListChangeSet<TDestination>, TKey, IChangeSet<TDestination>, IChangeSet<TDestination>>
    {
        private readonly ChangeSetMergeTracker<TDestination> _changeSetMergeTracker = new();

        public Subscription(
            IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<IChangeSet<TDestination>>> selector,
            IObserver<IChangeSet<TDestination>> observer,
            IEqualityComparer<TDestination>? equalityComparer)
            : base(observer)
        {
            // RemoveIndex outside of the Lock, but add locking before going to ClonedChangeSet so the contents are protected
            CreateParentSubscription(source.Transform((obj, key) =>
                new ClonedListChangeSet<TDestination>(MakeChildObservable(selector(obj, key).RemoveIndex()), equalityComparer)));
        }

        protected override void ParentOnNext(IChangeSet<ClonedListChangeSet<TDestination>, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one
                    // Remove any items from the previous list
                    case ChangeReason.Add or ChangeReason.Update:
                        AddChildSubscription(change.Current.Source, change.Key);
                        if (change.Previous.HasValue)
                        {
                            _changeSetMergeTracker.RemoveItems(change.Previous.Value.List);
                        }
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        RemoveChildSubscription(change.Key);
                        _changeSetMergeTracker.RemoveItems(change.Current.List);
                        break;
                }
            }
        }

        protected override void ChildOnNext(IChangeSet<TDestination> child, TKey parentKey) =>
            _changeSetMergeTracker.ProcessChangeSet(child, null);

        protected override void EmitChanges(IObserver<IChangeSet<TDestination>> observer) =>
            _changeSetMergeTracker.EmitChanges(observer);
    }
}
