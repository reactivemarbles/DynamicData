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
