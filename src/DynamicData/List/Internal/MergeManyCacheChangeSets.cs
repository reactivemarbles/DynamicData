// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Cache.Internal;

namespace DynamicData.List.Internal;

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>
    where TObject : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    private readonly IObservable<IChangeSet<TObject>> _source;

    private readonly Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> _changeSetSelector;

    private readonly IComparer<TDestination>? _comparer;

    private readonly IEqualityComparer<TDestination>? _equalityComparer;

    public MergeManyCacheChangeSets(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
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

                // Transform to an observable list of merge containers.
                var sourceListOfCaches = _source
                                            .WhereReasonsAreNot(ListChangeReason.Moved, ListChangeReason.Refresh)
                                            .Synchronize(locker)
                                            .Transform(obj => new MergedCacheChangeTracker<TDestination, TDestinationKey>.MergeContainer(_changeSetSelector(obj)))
                                            .AsObservableList();

                var shared = sourceListOfCaches.Connect().Publish();

                // this is manages all of the changes
                var changeTracker = new MergedCacheChangeTracker<TDestination, TDestinationKey>(() => sourceListOfCaches.Items.ToArray(), _comparer, _equalityComparer);

                // when a source item is removed, all of its sub-items need to be removed
                var removedItems = shared
                    .OnItemRemoved(mc => changeTracker.RemoveItems(mc.Cache.KeyValues, observer))
                    .Subscribe();

                // merge the items back together
                var allChanges = shared.MergeMany(mc => mc.Source)
                                                 .Synchronize(locker)
                                                 .Subscribe(
                                                        changes => changeTracker.ProcessChangeSet(changes, observer),
                                                        observer.OnError,
                                                        observer.OnCompleted);

                return new CompositeDisposable(sourceListOfCaches, allChanges, removedItems, shared.Connect());
            });
    }
}
