// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
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
            var locker = new object();
            var pendingUpdates = 0;

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TObject>();

            // Merge all of the changeset streams together and Process them with the change tracker which will emit the results
            return source.Select(src => new ChangeSet<ClonedListChangeSet<TObject>>(new[] { CreateChange(src) }))
                .Synchronize(locker)
                .MergeMany(clonedList => clonedList.Source.RemoveIndex().Do(static _ => { }, observer.OnError))
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, Interlocked.Decrement(ref pendingUpdates) == 0 ? observer : null),
                    observer.OnError,
                    observer.OnCompleted);

            // Always increment the counter OUTSIDE of the lock to signal any thread currently holding the lock
            // to not emit the changeset because more changes are incoming.
            IObservable<IChangeSet<TObject>> CreateChildObservable(IObservable<IChangeSet<TObject>> src) =>
                src.Do(_ => Interlocked.Increment(ref pendingUpdates)).Synchronize(locker!);

            // Can optimize for the Add case because that's the only one that applies
            Change<ClonedListChangeSet<TObject>> CreateChange(IObservable<IChangeSet<TObject>> source) =>
                new(ListChangeReason.Add, new ClonedListChangeSet<TObject>(CreateChildObservable(source), equalityComparer));
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
}
