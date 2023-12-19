using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.Tests.Utilities;

internal static class StressAddRemoveExtensions
{
    // This is the general case that can be used to create Add/Remove stress tests for anything
    public static IObservable<T> StressAddRemove<T, TState>(this IObservable<T> items, TState state, Action<T, TState> onAdd, Action<T, TState> onRemove, Func<T, TimeSpan?> getRemoveTimeout, IScheduler? scheduler = null)
        where T : notnull =>
            items.Do(i => onAdd(i, state))
                 .SelectMany(item => getRemoveTimeout?.Invoke(item) is TimeSpan ts
                    ? Observable.Timer(ts, scheduler ?? DefaultScheduler.Instance)
                                .Do(_ => onRemove(item, state))
                                .Select(_ => item)
                    : Observable.Return(item));

    // This is the Cache Specific version
    public static IObservable<T> StressAddRemove<T, TKey>(this IObservable<T> items, ISourceCache<T, TKey> cache, Func<T, TimeSpan?> getRemoveTimeout, IScheduler scheduler)
        where T : notnull
        where TKey : notnull =>
        StressAddRemove(items, cache, (i, c) => c.AddOrUpdate(i), (i, c) => c.Remove(i), getRemoveTimeout, scheduler);

    // This is the List Specific version
    public static IObservable<T> StressAddRemove<T>(this IObservable<T> items, ISourceList<T> list, Func<T, TimeSpan?> getRemoveTimeout, IScheduler scheduler)
        where T : notnull =>
        StressAddRemove(items, list, (i, l) => l.Add(i), (i, l) => l.Remove(i), getRemoveTimeout, scheduler);

    // This is the List version that uses explicit changesets
    public static IObservable<IChangeSet<T>> StressAddRemoveExplicit<T>(this IObservable<T> source, Func<T, TimeSpan?> getRemoveTime, IScheduler? scheduler = null)
        where T : notnull =>
        Observable.Create<IChangeSet<T>>(observer =>
        {
            void OnAdd(T t, IObserver<IChangeSet<T>> obs) =>
                obs.OnNext(new ChangeSet<T>(new[] { new Change<T>(ListChangeReason.Add, t) }));

            void OnRemove(T t, IObserver<IChangeSet<T>> obs) =>
                obs.OnNext(new ChangeSet<T>(new[] { new Change<T>(ListChangeReason.Remove, t) }));

            return source.StressAddRemove(observer, OnAdd, OnRemove, getRemoveTime, scheduler)
                        .Subscribe(_ => { }, observer.OnError, observer.OnCompleted);
        });

    // This is the List version that uses several explicit changesets firing together
    public static IObservable<IChangeSet<T>> StressAddRemoveExplicit<T>(this IObservable<T> source, int parallel, Func<T, TimeSpan?> getRemoveTime, IScheduler? scheduler = null)
        where T : notnull =>
        Observable.Merge(Enumerable.Range(0, parallel)
                                        .Select(_ => source.StressAddRemoveExplicit(getRemoveTime, scheduler)));
}
