using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace DynamicData.Tests.Utilities;

internal sealed class FakeScheduler
    : IScheduler
{
    private readonly List<ScheduledAction> _scheduledActions;

    private DateTimeOffset _now;

    public FakeScheduler()
        => _scheduledActions = new();

    public List<ScheduledAction> ScheduledActions
        => _scheduledActions;

    public DateTimeOffset Now
    {
        get => _now;
        set => _now = value;
    }

    public IDisposable Schedule<TState>(
            TState state,
            Func<IScheduler, TState, IDisposable> action)
        => ScheduleCore(
            state: state,
            dueTime: null,
            action: action);
    
    public IDisposable Schedule<TState>(
            TState state,
            TimeSpan dueTime,
            Func<IScheduler, TState, IDisposable> action)
        => ScheduleCore(
            state: state,
            dueTime: _now + dueTime,
            action: action);
    
    public IDisposable Schedule<TState>(
            TState state,
            DateTimeOffset dueTime,
            Func<IScheduler, TState, IDisposable> action)
        => ScheduleCore(
            state: state,
            dueTime: dueTime,
            action: action);

    // Simulate the scheduler invoking actions, allowing for each action to schedule followup actions, until none remain.
    public void SimulateUntilIdle(TimeSpan inaccuracyOffset = default)
    {
        while(ScheduledActions.Count is not 0)
        {
            // If the action doesn't have a DueTime, invoke it immediately
            if (ScheduledActions[0].DueTime is not DateTimeOffset dueTime)
            {
                ScheduledActions[0].Invoke();
                ScheduledActions.RemoveAt(0);
            }
            // If the action has a DueTime, invoke it when that time is reached, including inaccuracy, if given.
            // Inaccuracy is simulated by comparing each dueTime against an "effective clock" offset by the desired inaccuracy
            // E.G. if the given inaccuracy offset is -1ms, we want to simulate the scheduler invoking actions 1ms early.
            // For a dueTime of 10ms, that means we'd want to invoke when the clock is 9ms, so we need to the "effective clock" to be 10ms,
            // and need to subtract the -1ms to get there.
            else if (dueTime <= (Now - inaccuracyOffset))
            {
                ScheduledActions[0].Invoke();
                ScheduledActions.RemoveAt(0);
            }

            // Advance time by at least one tick after every action, to eliminate infinite-looping
            Now += TimeSpan.FromTicks(1);
        }
    }

    private IDisposable ScheduleCore<TState>(
        TState state,
        DateTimeOffset? dueTime,
        Func<IScheduler, TState, IDisposable> action)
    {
        var scheduledAction = new ScheduledAction(
            dueTime: dueTime,
            onInvoked: () => action.Invoke(this, state));

        _scheduledActions.Add(scheduledAction);

        return Disposable.Create(scheduledAction.Cancel);
    }

    public sealed class ScheduledAction
    {
        private readonly Func<IDisposable> _onInvoked;

        private DateTimeOffset? _dueTime;
        private bool _hasBeenCancelled;

        public ScheduledAction(
            DateTimeOffset? dueTime,
            Func<IDisposable> onInvoked)
        {
            _dueTime = dueTime;
            _onInvoked = onInvoked;
        }

        public DateTimeOffset? DueTime
            => _dueTime;

        public bool HasBeenCancelled
            => _hasBeenCancelled;

        public void Cancel()
            => _hasBeenCancelled = true;

        public IDisposable Invoke()
            => _onInvoked.Invoke();
    }
}
