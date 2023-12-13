using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;

namespace DynamicData.Tests.Utilities;


internal class AddRemoveStressTester<TItem>(int testCount, Func<TItem> addEvent, Action<TItem>? removeEvent, Action? completeEvent, Func<TimeSpan>? addDelay, Func<TimeSpan>? removeDelay)
    : AddRemoveStressTester<TItem, Unit>(testCount, Unit.Default, _ => addEvent(), (s, i) => removeEvent?.Invoke(i), _ => completeEvent?.Invoke(), addDelay, removeDelay)
    where TItem : notnull
{
}

internal class AddRemoveStressTester<TItem, TState>(int testCount, TState state, Func<TState, TItem> addEvent, Action<TState, TItem>? removeEvent, Action<TState>? completeEvent, Func<TimeSpan>? addDelay, Func<TimeSpan>? removeDelay)
    where TItem : notnull
    where TState : notnull
{
    private int _added;
    private int _removed;
    private int _done;
    private readonly int _count = testCount;
    private readonly TState _state = state;
    private readonly Func<TState, TItem> _addEvent = addEvent ?? throw new ArgumentNullException(nameof(addEvent));
    private readonly Action<TState, TItem>? _removeEvent = removeEvent;
    private readonly Action<TState>? _completeEvent = completeEvent;

    public IDisposable Start(IScheduler sch) => new CompositeDisposable(ScheduleAdd(sch), Disposable.Create(InvokeComplete));

    public IDisposable Start(int simultanenous, IScheduler sch) =>
        new CompositeDisposable(Enumerable.Range(0, simultanenous).Select(_ => ScheduleAdd(sch))
                                            .Append(Disposable.Create(InvokeComplete)));

    private IDisposable ScheduleAdd(IScheduler sch)
    {
        var current = Volatile.Read(ref _added);
        var expected = 0;

        do
        {
            if (current >= _count)
            {
                return Disposable.Empty;
            }
            expected = current;
        }
        while ((current = Interlocked.CompareExchange(ref _added, current + 1, current)) != expected);

        return sch.Schedule(this, NextAddTime(), (sch, tester) => tester.Add(sch));
    }

    private IDisposable Add(IScheduler sch)
    {
        var item = _addEvent(_state);
        var removeDisposable = sch.Schedule(NextRemoveTime(), () => Remove(item));
        return new CompositeDisposable(removeDisposable, ScheduleAdd(sch));
    }

    private void Remove(TItem item)
    {
        _removeEvent?.Invoke(_state, item);
        CheckComplete();
    }

    private void CheckComplete()
    {
        if (Interlocked.Increment(ref _removed) == _count)
        {
            InvokeComplete();
        }
    }

    private void InvokeComplete()
    {
        if (Interlocked.CompareExchange(ref _done, 1, 0) == 0)
        {
            _completeEvent?.Invoke(_state);
        }
    }

    private TimeSpan NextAddTime() => addDelay?.Invoke() ?? TimeSpan.Zero;

    private TimeSpan NextRemoveTime() => removeDelay?.Invoke() ?? NextAddTime();
}
