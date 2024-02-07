// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class GroupOnObservable<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> selectGroup)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() => Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(observer =>
    {
        var cache = new ChangeAwareCache<ManagedGroup<TObject, TKey, TGroupKey>, TGroupKey>();
        var locker = new object();
        var parentUpdate = false;

        // Helper to emit any pending changes when appropriate
        void EmitChanges(bool fromParent)
        {
            if (fromParent || !parentUpdate)
            {
                var changes = cache!.CaptureChanges();
                if (changes.Count > 0)
                {
                    var groupChanges = new GroupChangeSet<TObject, TKey, TGroupKey>(changes.Transform(mg => mg as IGroup<TObject, TKey, TGroupKey>));

                    observer.OnNext(groupChanges);
                }

                parentUpdate = false;
            }
        }

        // Create the sub-observable that takes the result of the transformation,
        // filters out unchanged values, and then updates the cache
        IObservable<TDestination> CreateSubObservable(TObject obj, TKey key) =>
            transform(obj, key)
                .DistinctUntilChanged()
                .Synchronize(locker!)
                .Do(val => cache!.AddOrUpdate(val, key));

        // Flag a parent update is happening once inside the lock
        var shared = source
            .Synchronize(locker!)
            .Do(_ => parentUpdate = true)
            .Publish();

        // MergeMany automatically handles Add/Update/Remove and OnCompleted/OnError correctly
        var subMerged = shared
            .MergeMany(CreateSubObservable)
            .SubscribeSafe(_ => EmitChanges(fromParent: false), observer.OnError, observer.OnCompleted);

        // Subscribe to the shared Observable to handle Remove events.  MergeMany will unsubscribe from the sub-observable,
        // but the corresponding key value needs to be removed from the Cache so the remove is observed downstream.
        var subRemove = shared
            .OnItemRemoved((_, key) => cache!.Remove(key), invokeOnUnsubscribe: false)
            .SubscribeSafe(_ => EmitChanges(fromParent: true), observer.OnError);

        return new CompositeDisposable(shared.Connect(), subMerged, subRemove);
    });

    private IObservable<(TGroupKey Current, Optional<TGroupKey> Last)> CreateGrouperSubscription(TObject obj, TKey key) =>
        selectGroup(obj, key).Publish(shared =>
            shared.Zip(
                shared.Select(val => Optional.Some(val)).StartWith(Optional.None<TGroupKey>()),
                (curr, last) => (curr, last)));

    private class GroupedProxy : IDisposable
    {
        private readonly IDisposable _disposable;

        public GroupedProxy(TObject obj, TKey key, IObservable<TGroupKey> groupObservable)
        {
            var sharedGroupObservable = groupObservable.Do(val => GroupKey = Optional.Some(val)).Publish();

            Object = obj;
            Key = key;
            GroupObservable = sharedGroupObservable;
            _disposable = sharedGroupObservable.Connect();
        }

        public TObject Object { get; }

        public TKey Key { get; }

        public Optional<TGroupKey> GroupKey { get; private set; }

        public IObservable<TGroupKey> GroupObservable { get; }

        public void Dispose() => _disposable.Dispose();
    }
}
