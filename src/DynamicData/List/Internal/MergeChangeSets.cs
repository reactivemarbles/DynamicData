// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.List.Internal;

/// <summary>
/// Operator that is similiar to Merge but intelligently handles List ChangeSets.
/// </summary>
internal sealed class MergeChangeSets<TObject>(IObservable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
{
    public MergeChangeSets(IEnumerable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer, bool completable, IScheduler? scheduler = null)
        : this(CreateObservable(source, completable, scheduler), equalityComparer)
    {
    }

    public IObservable<IChangeSet<TObject>> Run() => Observable.Create<IChangeSet<TObject>>(
        observer =>
        {
            // SharedDeliveryQueue + SynchronizeSafe replaces Synchronize(locker) so the
            // gate is released before downstream OnNext. Closes the cross-cache deadlock
            // window.
            var queue = new SharedDeliveryQueue();

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TObject>();

            // Merge all of the changeset streams together and Process them with the change tracker which will emit the results
            var publisher = CreateClonedListObservable(source, queue)
                .SynchronizeSafe(queue)
                .MergeMany(clonedList => clonedList.Source.RemoveIndex().Do(static _ => { }, observer.OnError))
                .Subscribe(
                    changes => changeTracker.ProcessChangeSet(changes, observer),
                    observer.OnError,
                    observer.OnCompleted);

            return new CompositeDisposable(publisher, queue);
        });

    private static IObservable<IObservable<IChangeSet<TObject>>> CreateObservable(IEnumerable<IObservable<IChangeSet<TObject>>> source, bool completable, IScheduler? scheduler)
    {
        var obs = (scheduler != null) ? source.ToObservable(scheduler) : source.ToObservable();

        if (!completable)
        {
            obs = obs.Concat(Observable.Never<IObservable<IChangeSet<TObject>>>());
        }

        return obs;
    }

    private Change<ClonedListChangeSet<TObject>> CreateChange(IObservable<IChangeSet<TObject>> source, SharedDeliveryQueue queue) =>
        new(ListChangeReason.Add, new ClonedListChangeSet<TObject>(source.SynchronizeSafe(queue), equalityComparer));

    // Create a ChangeSet Observable that produces ChangeSets with a single Add event for each new sub-observable
    private IObservable<IChangeSet<ClonedListChangeSet<TObject>>> CreateClonedListObservable(IObservable<IObservable<IChangeSet<TObject>>> source, SharedDeliveryQueue queue) =>
        source.Select(src => new ChangeSet<ClonedListChangeSet<TObject>>(new[] { CreateChange(src, queue) }));
}
