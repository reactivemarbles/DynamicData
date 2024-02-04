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
            var locker = new object();
            var cache = new Cache<ChangeSetCache<TObject, TKey>, int>();

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TObject, TKey>(() => cache.Items, comparer, equalityComparer);

            // Create a ChangeSet of Caches, synchronize, update the local copy, and merge the sub-observables together.
            return CreateContainerObservable(source, locker)
                .Synchronize(locker)
                .Do(cache.Clone)
                .MergeMany(mc => mc.Source.Do(static _ => { }, observer.OnError))
                .SubscribeSafe(
                    changes => changeTracker.ProcessChangeSet(changes, observer),
                    observer.OnError,
                    observer.OnCompleted);
        });

    // Can optimize for the Add case because that's the only one that applies
    private static Change<ChangeSetCache<TObject, TKey>, int> CreateChange(IObservable<IChangeSet<TObject, TKey>> source, int index, object locker) =>
        new(ChangeReason.Add, index, new ChangeSetCache<TObject, TKey>(source.Synchronize(locker)));

    // Create a ChangeSet Observable that produces ChangeSets with a single Add event for each new sub-observable
    private static IObservable<IChangeSet<ChangeSetCache<TObject, TKey>, int>> CreateContainerObservable(IObservable<IObservable<IChangeSet<TObject, TKey>>> source, object locker) =>
        source.Select((src, index) => new ChangeSet<ChangeSetCache<TObject, TKey>, int>(new[] { CreateChange(src, index, locker) }));

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
