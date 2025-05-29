using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Reactive.Testing;

namespace DynamicData.Tests.Utilities;

// Using a custom implementing of IObserver<> to bypass normal RX safeguards, allowing invalid behaviors to be potentially tested for.
public abstract class RecordingObserverBase<T>
    : IObserver<T>
{
    private readonly TaskCompletionSource _finalizationSource;
    private readonly List<Recorded<Notification<T>>> _notifications;
    private readonly IScheduler _scheduler;

    private Exception? _error;
    private bool _hasCompleted;

    protected RecordingObserverBase(IScheduler scheduler)
    {
        _finalizationSource = new();
        _notifications = new();
        _scheduler = scheduler;
    }

    public Exception? Error
        => _error;

    public bool HasCompleted
        => _hasCompleted;

    public bool HasFinalized
        => _hasCompleted || (_error is not null);

    public IReadOnlyList<Recorded<Notification<T>>> Notifications
        => _notifications;

    public async Task WaitForFinalizationAsync(TimeSpan timeout)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);

        await await Task.WhenAny(
            _finalizationSource.Task,
            Task.Delay(Timeout.Infinite, timeoutSource.Token));
    }

    protected abstract void OnNext(T value);

    void IObserver<T>.OnCompleted()
    {
        _notifications.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnCompleted<T>()));

        _hasCompleted = true;
        _finalizationSource.SetResult();
    }
    
    void IObserver<T>.OnError(Exception error)
    {
        _notifications.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnError<T>(error)));

        if (!HasFinalized)
            _error = error;
        _finalizationSource.SetResult();
    }

    void IObserver<T>.OnNext(T value)
    {
        _notifications.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnNext(value)));

        OnNext(value);
    }
}
