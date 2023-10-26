// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    private readonly Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> _changeSetSelector;

    private readonly IComparer<TDestination>? _comparer;

    private readonly IEqualityComparer<TDestination>? _equalityComparer;

    public MergeManyCacheChangeSets(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    {
        _source = source;
        _changeSetSelector = selector;
        _comparer = comparer;
        _equalityComparer = equalityComparer;
    }

    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run()
    {
        return Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
            observer =>
            {
                var locker = new object();

                // Transform to an observable cache of merge containers.
                var sourceCacheOfCaches = _source
                                            .IgnoreSameReferenceUpdate()
                                            .WhereReasonsAre(ChangeReason.Add, ChangeReason.Remove, ChangeReason.Update)
                                            .Synchronize(locker)
                                            .Transform((obj, key) => new ChangeSetMergeContainer<TDestination, TDestinationKey>(_changeSetSelector(obj, key)))
                                            .AsObservableCache();

                var shared = sourceCacheOfCaches.Connect().Publish();

                // this is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => sourceCacheOfCaches.Items.ToArray(), _comparer, _equalityComparer);

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

                return new CompositeDisposable(sourceCacheOfCaches, allChanges, removedItems, shared.Connect());
            });
    }
}
