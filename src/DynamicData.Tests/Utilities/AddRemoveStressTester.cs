using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;

namespace DynamicData.Tests.Utilities;

internal static class AddRemoveStressTester
{
    public static AddRemoveStressTester<TItem, TState> Create<TItem, TState>(Func<TState, TItem> addEvent, Action<TState, TItem>? removeEvent, Action<TState>? completeEvent, Func<TState, TimeSpan>? addDelay, Func<TState, TimeSpan>? removeDelay)
        where TItem : notnull
        where TState : notnull =>
        new AddRemoveStressTester<TItem, TState>(addEvent, removeEvent, completeEvent, addDelay, removeDelay);

    public static AddRemoveStressTester<TItem> Create<TItem>(Func<TItem> addEvent, Action<TItem>? removeEvent, Action? completeEvent, Func<TimeSpan>? addDelay, Func<TimeSpan>? removeDelay)
        where TItem : notnull =>
        new AddRemoveStressTester<TItem>(addEvent, removeEvent, completeEvent, addDelay, removeDelay);
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
