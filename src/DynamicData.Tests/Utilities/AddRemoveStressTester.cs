using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.Tests.Utilities;

internal static class AddRemoveStressTester
{
    public static IObservable<T> StressAddRemove<T, TKey>(this IObservable<T> items, ISourceCache<T, TKey> cache, Func<T, TimeSpan?> getRemoveTimeout, IScheduler scheduler)
        where T : notnull
        where TKey : notnull =>
        StressAddRemove(items, cache, (i, c) => c.AddOrUpdate(i), (i, c) => c.Remove(i), getRemoveTimeout, scheduler);


    public static IObservable<T> StressAddRemove<T>(this IObservable<T> items, ISourceList<T> list, Func<T, TimeSpan?> getRemoveTimeout, IScheduler scheduler)
        where T : notnull =>
        StressAddRemove(items, list, (i, l) => l.Add(i), (i, l) => l.Remove(i), getRemoveTimeout, scheduler);

    public static IObservable<IChangeSet<T>> StressAddRemoveExplicit<T>(this IObservable<T> source, int parallel, Func<T, TimeSpan?> getRemoveTime, IScheduler? scheduler = null)
        where T : notnull =>
        Observable.Create<IChangeSet<T>>(observer =>
        {
            void OnAdd(T t, IObserver<IChangeSet<T>> obs) =>
                obs.OnNext(new ChangeSet<T>(new[] { new Change<T>(ListChangeReason.Add, t) }));

            void OnRemove(T t, IObserver<IChangeSet<T>> obs) =>
                obs.OnNext(new ChangeSet<T>(new[] { new Change<T>(ListChangeReason.Remove, t) }));

            var addRemoveObservable = source.StressAddRemove(observer, OnAdd, OnRemove, getRemoveTime, scheduler);

            // This will cause the add/remove events to fire in an unsafe (non-Rx compliant) way, but for extreme stress testing...
            return Observable.Merge(Enumerable.Repeat(addRemoveObservable, parallel))
                            .Subscribe(_ => { }, observer.OnError, observer.OnCompleted);
        });

    public static IObservable<T> StressAddRemove<T, TState>(this IObservable<T> items, TState state, Action<T, TState> onAdd, Action<T, TState> onRemove, Func<T, TimeSpan?> getRemoveTimeout, IScheduler? scheduler = null)
        where T : notnull =>
            items.Do(i => onAdd(i, state))
                 .SelectMany(item => getRemoveTimeout?.Invoke(item) is TimeSpan ts
                    ? Observable.Timer(ts, scheduler ?? DefaultScheduler.Instance)
                                .Do(_ => onRemove(item, state))
                                .Select(_ => item)
                    : Observable.Empty(item));

    public static IObservable<IChangeSet<TItem>> AddRemoveChangeSet<TItem>(this IObservable<TItem> items, IScheduler scheduler, Func<TItem, TimeSpan?> getRemoveTimeout)
        where TItem : notnull =>
        ObservableChangeSet.Create<TItem>(list =>
                items.Do(list.Add)
                    .SelectMany(item => getRemoveTimeout?.Invoke(item) is TimeSpan ts
                        ? Observable.Timer(ts, scheduler).Do(_ => list.Remove(item))
                        : Observable.Empty<long>())
                    .Do(_ => { }, () => list.Dispose())
                    .Subscribe());

    public static IObservable<IChangeSet<TItem, TKey>> AddRemoveChangeSet<TItem, TKey>(this IObservable<TItem> items, Func<TItem, TKey> keySelector, IScheduler scheduler, Func<TItem, TimeSpan?> getRemoveTimeout)
        where TItem : notnull
        where TKey : notnull =>
        ObservableChangeSet.Create(cache =>
            {
                return items.Do(i => cache.AddOrUpdate(i))
                    .SelectMany(item => getRemoveTimeout?.Invoke(item) is TimeSpan ts
                        ? Observable.Timer(ts, scheduler).Do(_ => cache.Remove(item))
                        : Observable.Empty<long>())
                    .Do(_ => { }, () => cache.Dispose())
                    .Subscribe();
            },
            keySelector);

    public static AddRemoveStressTester<TItem, TState> Create<TItem, TState>(Func<TState, TItem> addEvent, Action<TState, TItem>? removeEvent, Action<TState>? completeEvent, Func<TState, TimeSpan>? addDelay, Func<TState, TimeSpan>? removeDelay)
        where TItem : notnull
        where TState : notnull =>
        new AddRemoveStressTester<TItem, TState>(addEvent, removeEvent, completeEvent, addDelay, removeDelay);

    public static AddRemoveStressTester<TItem> Create<TItem>(Func<TItem> addEvent, Action<TItem>? removeEvent, Action? completeEvent, Func<TimeSpan>? addDelay, Func<TimeSpan>? removeDelay)
        where TItem : notnull =>
        new AddRemoveStressTester<TItem>(addEvent, removeEvent, completeEvent, addDelay, removeDelay);

    public static AddRemoveStressTester<TItem, ISourceList<TItem>> Create<TItem>(Func<TItem> createItem, Predicate<TItem>? shouldRemove, Action<ISourceList<TItem>>? completeEvent, Func<ISourceList<TItem>, TimeSpan>? addDelay, Func<ISourceList<TItem>, TimeSpan>? removeDelay)
        where TItem : notnull =>
        new (
            list => createItem().With(list.Add),
            GetListRemove(shouldRemove),
            completeEvent,
            addDelay,
            removeDelay);

    public static AddRemoveStressTester<TItem, ISourceCache<TItem, TKey>> Create<TItem, TKey>(Func<TItem> createItem, Predicate<TItem>? shouldRemove, Action<ISourceCache<TItem, TKey>>? completeEvent, Func<ISourceCache<TItem, TKey>, TimeSpan>? addDelay, Func<ISourceCache<TItem, TKey>, TimeSpan>? removeDelay)
        where TItem : notnull
        where TKey : notnull =>
        new(
            cache => createItem().With(cache.AddOrUpdate),
            GetCacheRemove<TItem, TKey>(shouldRemove),
            completeEvent,
            addDelay,
            removeDelay);

    private static Action<ISourceCache<TItem, TKey>, TItem> GetCacheRemove<TItem, TKey>(Predicate<TItem>? pred)
        where TItem : notnull
        where TKey : notnull =>
        (cache, item) =>
        {
            if (pred?.Invoke(item) ?? true)
            {
                cache.Remove(item);
            }
        };

    private static Action<ISourceList<TItem>, TItem> GetListRemove<TItem>(Predicate<TItem>? pred) where TItem : notnull =>
        (list, item) =>
        {
            if (pred?.Invoke(item) ?? true)
            {
                list.Remove(item);
            }
        };
}

internal class AddRemoveStressTester<TItem>(Func<TItem> addEvent, Action<TItem>? removeEvent, Action? completeEvent, Func<TimeSpan>? addDelay, Func<TimeSpan>? removeDelay)
    : AddRemoveStressTester<TItem, Unit>(_ => addEvent(), (s, i) => removeEvent?.Invoke(i), _ => completeEvent?.Invoke(), (addDelay != null) ? _ => addDelay() : null, (removeDelay != null) ? _ => removeDelay() : null)
    where TItem : notnull
{
    public IDisposable Start(IScheduler sch, int count, int parallel = 1) => Start(sch, Unit.Default, count, parallel);
}

internal class AddRemoveStressTester<TItem, TState>(Func<TState, TItem> addEvent, Action<TState, TItem>? removeEvent, Action<TState>? completeEvent, Func<TState, TimeSpan>? addDelay, Func<TState, TimeSpan>? removeDelay)
    where TItem : notnull
    where TState : notnull
{
    private class Tester(int count, TState state)
    {
        private int _added;
        private int _removed;
        private int _done;

        public TState State => state;

        public bool CheckCount()
        {
            var current = Volatile.Read(ref _added);
            var expected = 0;

            do
            {
                if (current >= count)
                {
                    return false;
                }
                expected = current;
            }
            while ((current = Interlocked.CompareExchange(ref _added, current + 1, current)) != expected);
            return true;
        }

        public bool MarkRemoved() => Interlocked.Increment(ref _removed) == count;

        public bool CheckComplete() => Interlocked.CompareExchange(ref _done, 1, 0) == 0;
    }

    private readonly Func<TState, TItem> _addEvent = addEvent ?? throw new ArgumentNullException(nameof(addEvent));
    private readonly Action<TState, TItem>? _removeEvent = removeEvent;
    private readonly Action<TState>? _completeEvent = completeEvent;

    public IDisposable Start(IScheduler sch, TState state, int count, int parallel = 1) =>
        new CompositeDisposable((parallel, new Tester(count, state)) switch
        {
            (>1, Tester t) => Enumerable.Range(0, parallel).Select(_ => ScheduleAdd(sch, t)).Append(CreateCompleter(t)),
            (_, Tester t) => new[] { ScheduleAdd(sch, t), CreateCompleter(t) },
        });

    private IDisposable ScheduleAdd(IScheduler sch, Tester tester) =>
        tester.CheckCount() ? sch.Schedule(tester, NextAddTime(tester.State), Add) : Disposable.Empty;

    private IDisposable Add(IScheduler sch, Tester tester)
    {
        var item = _addEvent(tester.State);
        var removeDisposable = sch.Schedule(NextRemoveTime(tester.State), () => Remove(tester, item));
        return new CompositeDisposable(removeDisposable, ScheduleAdd(sch, tester));
    }

    private void Remove(Tester tester, TItem item)
    {
        _removeEvent?.Invoke(tester.State, item);
        CheckComplete(tester);
    }

    private IDisposable CreateCompleter(Tester tester) => Disposable.Create(() => InvokeComplete(tester));

    private void InvokeComplete(Tester tester)
    {
        if (tester.CheckComplete())
        {
            _completeEvent?.Invoke(tester.State);
        }
    }

    private void CheckComplete(Tester tester)
    {
        if (tester.MarkRemoved())
        {
            InvokeComplete(tester);
        }
    }

    private TimeSpan NextAddTime(TState state) => addDelay?.Invoke(state) ?? TimeSpan.Zero;

    private TimeSpan NextRemoveTime(TState state) => removeDelay?.Invoke(state) ?? NextAddTime(state);
}
