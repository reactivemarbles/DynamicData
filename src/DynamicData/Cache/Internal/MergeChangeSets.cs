// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to Merge but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeChangeSets<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer)
    where TObject : notnull
    where TKey : notnull
{
    public MergeChangeSets(IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer, bool completable, IScheduler? scheduler = null)
        : this(CreateObservable(source, completable, scheduler), equalityComparer, comparer)
    {
    }

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
        observer =>
        {
            var cache = new Cache<ChangeSetCache<TObject, TKey>, int>();
            var locker = new object();
            var pendingUpdates = 0;

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TObject, TKey>(() => cache.Items, comparer, equalityComparer);

            // Create a ChangeSet of Caches, synchronize, update the local copy, and merge the sub-observables together.
            return source
                .Select((src, index) => new ChangeSet<ChangeSetCache<TObject, TKey>, int>(new[] { CreateChange(src, index) }))
                .Synchronize(locker)
                .Do(cache.Clone)
                .MergeMany(mc => mc.Source.Do(static _ => { }, observer.OnError))
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, Interlocked.Decrement(ref pendingUpdates) == 0 ? observer : null),
                    observer.OnError,
                    observer.OnCompleted);

            // Always increment the counter OUTSIDE of the lock to signal any thread currently holding the lock
            // to not emit the changeset because more changes are incoming.
            IObservable<IChangeSet<TObject, TKey>> CreateChildObservable(IObservable<IChangeSet<TObject, TKey>> src) =>
                src.Do(_ => Interlocked.Increment(ref pendingUpdates)).Synchronize(locker!);

            // Can optimize for the Add case because that's the only one that applies
            Change<ChangeSetCache<TObject, TKey>, int> CreateChange(IObservable<IChangeSet<TObject, TKey>> source, int index) =>
                new(ChangeReason.Add, index, new ChangeSetCache<TObject, TKey>(CreateChildObservable(source)));
        });

    // Create a ChangeSet Observable with a single event that adds all the values in the enum (and then completes, maybe)
    private static IObservable<IObservable<IChangeSet<TObject, TKey>>> CreateObservable(IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, bool completable, IScheduler? scheduler = null)
    {
        var obs = (scheduler != null) ? source.ToObservable(scheduler) : source.ToObservable();

        if (!completable)
        {
            obs = obs.Concat(Observable.Never<IObservable<IChangeSet<TObject, TKey>>>());
        }

        return obs;
    }
}
