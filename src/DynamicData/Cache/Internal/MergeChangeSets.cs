// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Operator that is similiar to Merge but intelligently handles Cache ChangeSets.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
/// <param name="comparer">The comparer value.</param>
internal sealed class MergeChangeSets<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MergeChangeSets{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <param name="completable">The completable value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    public MergeChangeSets(IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer, bool completable, IScheduler? scheduler = null)
        : this(CreateObservable(source, completable, scheduler), equalityComparer, comparer)
    {
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
        observer =>
        {
            var queue = new SharedDeliveryQueue();
            var cache = new Cache<ChangeSetCache<TObject, TKey>, int>();

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TObject, TKey>(() => cache.Items, comparer, equalityComparer);

            // Create a ChangeSet of Caches, synchronize, update the local copy, and merge the sub-observables together.
            return new CompositeDisposable(
                PrimitivesLinqExtensions.SubscribeSafe(
                    CreateContainerObservable(source, queue)
                        .SynchronizeSafe(queue)
                        .Do(cache.Clone)
                        .MergeMany(mc => mc.Source.Do(static _ => { }, observer.OnError)),
                    changes => changeTracker.ProcessChangeSet(changes, observer),
                    observer.OnError,
                    observer.OnCompleted),
                queue);
        });
    // Can optimize for the Add case because that's the only one that applies

    /// <summary>
    /// Executes the CreateChange operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="index">The index value.</param>
    /// <param name="queue">The queue value.</param>
    /// <returns>The result of the operation.</returns>
    private static Change<ChangeSetCache<TObject, TKey>, int> CreateChange(IObservable<IChangeSet<TObject, TKey>> source, int index, SharedDeliveryQueue queue) =>
        new(ChangeReason.Add, index, new ChangeSetCache<TObject, TKey>(source.IgnoreSameReferenceUpdate().SynchronizeSafe(queue)));
    // Create a ChangeSet Observable that produces ChangeSets with a single Add event for each new sub-observable

    /// <summary>
    /// Executes the CreateContainerObservable operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="queue">The queue value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<ChangeSetCache<TObject, TKey>, int>> CreateContainerObservable(IObservable<IObservable<IChangeSet<TObject, TKey>>> source, SharedDeliveryQueue queue) =>
        source.Select((src, index) => new ChangeSet<ChangeSetCache<TObject, TKey>, int>(new[] { CreateChange(src, index, queue) }));
    // Create a ChangeSet Observable with a single event that adds all the values in the enum (and then completes, maybe)

    /// <summary>
    /// Executes the CreateObservable operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="completable">The completable value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    /// <returns>The result of the operation.</returns>
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
