// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOnObservable<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> selectGroup)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() =>
        Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(observer => new Subscription(source, selectGroup, observer));

    // Maintains state for a single subscription
    private sealed class Subscription : CacheParentSubscription<TObject, TKey, (TGroupKey, TObject), IGroupChangeSet<TObject, TKey, TGroupKey>>
    {
        private readonly DynamicGrouper<TObject, TKey, TGroupKey> _grouper = new();
        private readonly Func<TObject, TKey, IObservable<TGroupKey>> _selectGroup;

        public Subscription(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> selectGroup, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
            : base(observer)
        {
            _selectGroup = selectGroup;
            CreateParentSubscription(source);
        }

        protected override void ParentOnNext(IChangeSet<TObject, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                _grouper.ProcessChange(change);

                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the cache and emit the changes
                    case ChangeReason.Add or ChangeReason.Update:
                        AddGroupSubscription(change.Current, change.Key);
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        RemoveChildSubscription(change.Key);
                        break;
                }
            }
        }

        protected override void ChildOnNext((TGroupKey, TObject) tuple, TKey parentKey) =>
            _grouper.AddOrUpdate(parentKey, tuple.Item1, tuple.Item2);

        protected override void EmitChanges(IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer) =>
            _grouper.EmitChanges(observer);

        protected override void Dispose(bool disposing)
        {
            _grouper.Dispose();
            base.Dispose(disposing);
        }

        private void AddGroupSubscription(TObject obj, TKey key) =>
            AddChildSubscription(MakeChildObservable(_selectGroup(obj, key).DistinctUntilChanged().Select(groupKey => (groupKey, obj))), key);
    }

#if false
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(observer =>
    {
        var grouper = new DynamicGrouper<TObject, TKey, TGroupKey>();
        var locker = InternalEx.NewLock();
        var parentUpdate = false;

        IObservable<TGroupKey> CreateGroupObservable(TObject item, TKey key) =>
            selectGroup(item, key)
                .DistinctUntilChanged()
                .Synchronize(locker!)
                .Do(
                    onNext: groupKey => grouper!.AddOrUpdate(key, groupKey, item, !parentUpdate ? observer : null),
                    onError: observer.OnError);

        // Create a shared connection to the source
        var shared = source
            .Synchronize(locker)
            .Do(_ => parentUpdate = true)
            .Publish();

        // First process the changesets
        var subChanges = shared
            .SubscribeSafe(
                onNext: changeSet => grouper.ProcessChangeSet(changeSet),
                onError: observer.OnError);

        // Next process the Grouping observables created for each item
        var subMergeMany = shared
            .MergeMany(CreateGroupObservable)
            .SubscribeSafe(
                onError: observer.OnError,
                onCompleted: observer.OnCompleted);

        // Finally, emit the results
        var subResults = shared
            .SubscribeSafe(
                onNext: _ =>
                {
                    grouper.EmitChanges(observer);
                    parentUpdate = false;
                },
                onError: observer.OnError);

        return new CompositeDisposable(shared.Connect(), subMergeMany, subChanges, grouper);
    });
#endif
}
