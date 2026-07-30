// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Operator that is similiar to Merge but intelligently handles List ChangeSets.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
internal sealed class MergeChangeSets<TObject>(IObservable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MergeChangeSets{TObject}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="completable">The completable value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    public MergeChangeSets(IEnumerable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer, bool completable, IScheduler? scheduler = null)
        : this(CreateObservable(source, completable, scheduler), equalityComparer)
    {
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject>> Run() => Observable.Create<IChangeSet<TObject>>(
        observer =>
        {
            var locker = InternalEx.NewLock();

            // This is manages all of the changes
            var changeTracker = new ChangeSetMergeTracker<TObject>();

            // Merge all of the changeset streams together and Process them with the change tracker which will emit the results
            return CreateClonedListObservable(source, locker)
                .Synchronize(locker)
                .MergeMany(clonedList => clonedList.Source.RemoveIndex().Do(static _ => { }, observer.OnError))
                .Subscribe(
                    changes => changeTracker.ProcessChangeSet(changes, observer),
                    observer.OnError,
                    observer.OnCompleted);
        });

    /// <summary>
    /// Executes the CreateObservable operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="completable">The completable value.</param>
    /// <param name="scheduler">The scheduler value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IObservable<IChangeSet<TObject>>> CreateObservable(IEnumerable<IObservable<IChangeSet<TObject>>> source, bool completable, IScheduler? scheduler)
    {
        var obs = (scheduler != null) ? source.ToObservable(scheduler) : source.ToObservable();

        if (!completable)
        {
            obs = obs.Concat(Observable.Never<IObservable<IChangeSet<TObject>>>());
        }

        return obs;
    }
    // Can optimize for the Add case because that's the only one that applies
#if NET9_0_OR_GREATER

    /// <summary>
    /// Executes the CreateChange operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="locker">The locker value.</param>
    /// <returns>The result of the operation.</returns>
    private Change<ClonedListChangeSet<TObject>> CreateChange(IObservable<IChangeSet<TObject>> source, Lock locker) =>
        new(ListChangeReason.Add, new ClonedListChangeSet<TObject>(source.Synchronize(locker), equalityComparer));
    // Create a ChangeSet Observable that produces ChangeSets with a single Add event for each new sub-observable

    /// <summary>
    /// Executes the CreateClonedListObservable operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="locker">The locker value.</param>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<ClonedListChangeSet<TObject>>> CreateClonedListObservable(IObservable<IObservable<IChangeSet<TObject>>> source, Lock locker) =>
        source.Select(src => new ChangeSet<ClonedListChangeSet<TObject>>(new[] { CreateChange(src, locker) }));
#else

    /// <summary>
    /// Executes the CreateChange operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="locker">The locker value.</param>
    /// <returns>The result of the operation.</returns>
    private Change<ClonedListChangeSet<TObject>> CreateChange(IObservable<IChangeSet<TObject>> source, object locker) =>
        new(ListChangeReason.Add, new ClonedListChangeSet<TObject>(source.Synchronize(locker), equalityComparer));
    // Create a ChangeSet Observable that produces ChangeSets with a single Add event for each new sub-observable

    /// <summary>
    /// Executes the CreateClonedListObservable operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="locker">The locker value.</param>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<ClonedListChangeSet<TObject>>> CreateClonedListObservable(IObservable<IObservable<IChangeSet<TObject>>> source, object locker) =>
        source.Select(src => new ChangeSet<ClonedListChangeSet<TObject>>(new[] { CreateChange(src, locker) }));
#endif

}
