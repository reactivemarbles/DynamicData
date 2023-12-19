// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Operator that is similiar to Merge but intelligently handles Cache ChangeSets.
/// </summary>
internal sealed class MergeChangeSets<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<ChangeSetCache<TObject, TKey>, int>> _source;

    private readonly IComparer<TObject>? _comparer;

    private readonly IEqualityComparer<TObject>? _equalityComparer;

    public MergeChangeSets(IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer, bool completable, IScheduler? scheduler = null)
        : this(CreateContainerObservable(source, completable, scheduler), equalityComparer, comparer)
    {
    }

    public MergeChangeSets(IObservable<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer)
        : this(CreateContainerObservable(source), equalityComparer, comparer)
    {
    }

    private MergeChangeSets(IObservable<IChangeSet<ChangeSetCache<TObject, TKey>, int>> source, IEqualityComparer<TObject>? equalityComparer, IComparer<TObject>? comparer)
    {
        _source = source;
        _comparer = comparer;
        _equalityComparer = equalityComparer;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var locker = new object();

                // Create a local cache of Merge Containers
                var localCache = _source.Synchronize(locker).AsObservableCache();

                // Set up the change tracker
                var changeTracker = new ChangeSetMergeTracker<TObject, TKey>(() => localCache.Items, _comparer, _equalityComparer);

                // Merge all of the changeset streams together and Process them with the change tracker which will emit the results
                var subscription = localCache.Connect().MergeMany(mc => mc.Source.Do(static _ => { }, observer.OnError))
                                                        .Synchronize(locker)
                                                        .Subscribe(
                                                                changes => changeTracker.ProcessChangeSet(changes, observer),
                                                                observer.OnError,
                                                                observer.OnCompleted);

                return new CompositeDisposable(localCache, subscription);
            });

    // Can optimize for the Add case because that's the only one that applies
    private static Change<ChangeSetCache<TObject, TKey>, int> CreateChange(IObservable<IChangeSet<TObject, TKey>> source, int index) =>
        new(ChangeReason.Add, index, new ChangeSetCache<TObject, TKey>(source));

    // Create a ChangeSet Observable that produces ChangeSets with a single Add event for each new sub-observable
    private static IObservable<IChangeSet<ChangeSetCache<TObject, TKey>, int>> CreateContainerObservable(IObservable<IObservable<IChangeSet<TObject, TKey>>> source) =>
        source.Select((src, index) => new ChangeSet<ChangeSetCache<TObject, TKey>, int>(new[] { CreateChange(src, index) }));

    // Create a ChangeSet Observable with a single event that adds all the values in the enum (and then completes, maybe)
    private static IObservable<IChangeSet<ChangeSetCache<TObject, TKey>, int>> CreateContainerObservable(IEnumerable<IObservable<IChangeSet<TObject, TKey>>> source, bool completable, IScheduler? scheduler = null) =>
        Observable.Create<IChangeSet<ChangeSetCache<TObject, TKey>, int>>(observer =>
        {
            void EmitChanges()
            {
                observer.OnNext(new ChangeSet<ChangeSetCache<TObject, TKey>, int>(source.Select(CreateChange)));

                if (completable)
                {
                    observer.OnCompleted();
                }
            }

            if (scheduler is not null)
            {
                return scheduler.Schedule(EmitChanges);
            }

            EmitChanges();
            return Disposable.Empty;
        });
}
