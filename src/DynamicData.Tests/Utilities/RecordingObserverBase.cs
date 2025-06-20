﻿using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive;
using System.Threading.Tasks;

using Microsoft.Reactive.Testing;

namespace DynamicData.Tests.Utilities;

// Using a custom implementing of IObserver<> to bypass normal RX safeguards, allowing invalid behaviors to be potentially tested for.
public abstract class RecordingObserverBase<T>
    : IObserver<T>
{
    private readonly TaskCompletionSource _whenFinalizedSource;
    private readonly List<Recorded<Notification<T>>> _notifications;
    private readonly IScheduler _scheduler;

    private Exception? _error;
    private bool _hasCompleted;

    protected RecordingObserverBase(IScheduler scheduler)
    {
        _whenFinalizedSource = new();
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

    public Task WhenFinalized
        => _whenFinalizedSource.Task;

    protected abstract void OnNext(T value);

    void IObserver<T>.OnCompleted()
    {
        _notifications.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnCompleted<T>()));

        _hasCompleted = true;
        _whenFinalizedSource.SetResult();
    }
    
    void IObserver<T>.OnError(Exception error)
    {
        _notifications.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnError<T>(error)));

        if (!HasFinalized)
            _error = error;
        _whenFinalizedSource.SetResult();
    }

    void IObserver<T>.OnNext(T value)
    {
        _notifications.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnNext(value)));

        OnNext(value);
    }
}
