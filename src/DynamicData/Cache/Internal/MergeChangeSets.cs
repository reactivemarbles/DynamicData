// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to Merge but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeChangeSets<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly ICollection<IObservable<IChangeSet<TObject, TKey>>> _source;

    private readonly bool _completable;

    private readonly IComparer<TObject>? _comparer;

    private readonly IEqualityComparer<TObject>? _equalityComparer;

    public MergeChangeSets(ICollection<IObservable<IChangeSet<TObject, TKey>>> source, bool completable, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer)
    {
        _source = source;
        _completable = completable;
        _comparer = comparer;
        _equalityComparer = equalityComparer;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                // Create an array of MergeContainers
                var containers = _source.Select(source => new MergedCacheChangeTracker<TObject, TKey>.MergeContainer(source)).ToArray();

                // this is manages all of the changes
                var changeTracker = new MergedCacheChangeTracker<TObject, TKey>(() => containers, _comparer, _equalityComparer);

                // merge all of the changes together
                return containers.AsObservableChangeSet(completable: _completable)
                                .MergeMany(mc => mc.Source)
                                .Synchronize()
                                .Subscribe(
                                        changes => changeTracker.ProcessChangeSet(changes, observer),
                                        observer.OnError,
                                        observer.OnCompleted);
            });
    }
}
