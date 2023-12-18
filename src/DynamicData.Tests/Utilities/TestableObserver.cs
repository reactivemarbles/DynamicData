using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;

using Microsoft.Reactive.Testing;

namespace DynamicData.Tests.Utilities;

public static class TestableObserver
{
    public static TestableObserver<T> Create<T>(IScheduler? scheduler = null)
        => new(scheduler ?? DefaultScheduler.Instance);
}

// Not using any existing Observer class, or Observer.Create<T>() to bypass normal RX safeguards, which prevent out-of-sequence notifications.
public sealed class TestableObserver<T>
    : ITestableObserver<T>
{
    private readonly List<Recorded<Notification<T>>> _messages;
    private readonly IScheduler _scheduler;

    public TestableObserver(IScheduler scheduler)
    {
        _messages = new();
        _scheduler = scheduler;
    }

    public IList<Recorded<Notification<T>>> Messages
        => _messages;

    void IObserver<T>.OnCompleted()
        => _messages.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnCompleted<T>()));
    
    void IObserver<T>.OnError(Exception error)
        => _messages.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnError<T>(error)));

    void IObserver<T>.OnNext(T value)
        => _messages.Add(new(
            time: _scheduler.Now.Ticks,
            value: Notification.CreateOnNext(value)));
}
