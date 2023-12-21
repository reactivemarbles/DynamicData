// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

/// <summary>
/// Operator that is similiar to Merge but intelligently handles List ChangeSets.
/// </summary>
internal sealed class MergeChangeSets<TObject>
    where TObject : notnull
{
    private readonly IObservable<IChangeSet<ClonedListChangeSet<TObject>>> _source;

    private readonly IEqualityComparer<TObject>? _equalityComparer;

    public MergeChangeSets(IEnumerable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer, bool completable, IScheduler? scheduler = null)
    {
        _equalityComparer = equalityComparer;
        _source = CreateClonedListObservable(source, completable, scheduler);
    }

    public MergeChangeSets(IObservable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer)
    {
        _equalityComparer = equalityComparer;
        _source = CreateClonedListObservable(source);
    }

    public IObservable<IChangeSet<TObject>> Run() => Observable.Create<IChangeSet<TObject>>(
            observer =>
            {
                // This is manages all of the changes
                var changeTracker = new ChangeSetMergeTracker<TObject>();

                // Merge all of the changeset streams together and Process them with the change tracker which will emit the results
                return _source.MergeMany(clonedList => clonedList.Source.RemoveIndex().Do(static _ => { }, observer.OnError))
                            .Subscribe(
                                    changes => changeTracker.ProcessChangeSet(changes, observer),
                                    observer.OnError,
                                    observer.OnCompleted);
            });

    // Can optimize for the Add case because that's the only one that applies
    private Change<ClonedListChangeSet<TObject>> CreateChange(IObservable<IChangeSet<TObject>> source) =>
        new(ListChangeReason.Add, new ClonedListChangeSet<TObject>(source, _equalityComparer));

    // Create a ChangeSet Observable that produces ChangeSets with a single Add event for each new sub-observable
    private IObservable<IChangeSet<ClonedListChangeSet<TObject>>> CreateClonedListObservable(IObservable<IObservable<IChangeSet<TObject>>> source) =>
        source.Select(src => new ChangeSet<ClonedListChangeSet<TObject>>(new[] { CreateChange(src) }));

    // Create a ChangeSet Observable with a single event that adds all the values in the enum (and then completes, maybe)
    private IObservable<IChangeSet<ClonedListChangeSet<TObject>>> CreateClonedListObservable(IEnumerable<IObservable<IChangeSet<TObject>>> source, bool completable, IScheduler? scheduler = null)
    {
        // Create a changeset that has a change for each changeset in the enumerable
        var changeSet = new ChangeSet<ClonedListChangeSet<TObject>>(source.Select(CreateChange));

        // Create an observable that returns it (using the scheduler if provided)
        var observable =
            scheduler is IScheduler sch
                ? Observable.Return<IChangeSet<ClonedListChangeSet<TObject>>>(changeSet, sch)
                : Observable.Return(changeSet);

        // Block completion if it isn't supposed to complete
        return completable ? observable : observable.Concat(Observable.Never<IChangeSet<ClonedListChangeSet<TObject>>>());
    }
}
