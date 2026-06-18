using System.Diagnostics;

namespace DynamicData.Tests.Utilities;

public enum NotificationKind
{
    OnNext,
    OnError,
    OnCompleted
}

public sealed class Notification<T>
{
    private Notification(NotificationKind kind, T? value = default, Exception? exception = null)
    {
        Kind = kind;
        Value = value!;
        Exception = exception;
    }

    public NotificationKind Kind { get; }

    public T Value { get; }

    public Exception? Exception { get; }

    public static Notification<T> CreateOnNext(T value) => new(NotificationKind.OnNext, value);

    public static Notification<T> CreateOnError(Exception error) => new(NotificationKind.OnError, exception: error);

    public static Notification<T> CreateOnCompleted() => new(NotificationKind.OnCompleted);
}

public static class Notification
{
    public static Notification<T> CreateOnNext<T>(T value) => Notification<T>.CreateOnNext(value);

    public static Notification<T> CreateOnError<T>(Exception error) => Notification<T>.CreateOnError(error);

    public static Notification<T> CreateOnCompleted<T>() => Notification<T>.CreateOnCompleted();
}

public readonly record struct Recorded<T>
{
    public Recorded(long time, T value)
    {
        Time = time;
        Value = value;
    }

    public long Time { get; }

    public T Value { get; }
}

public sealed class NewThreadScheduler : ISequencer
{
    public DateTimeOffset Now => ThreadPoolSequencer.Instance.Now;

    public long Timestamp => ThreadPoolSequencer.Instance.Timestamp;

    public void Schedule(IWorkItem item) => ThreadPoolSequencer.Instance.Schedule(item);

    public void Schedule(IWorkItem item, long dueTimestamp) => ThreadPoolSequencer.Instance.Schedule(item, dueTimestamp);
}

public sealed class TestScheduler : ISequencer
{
    private readonly List<ScheduledItem> _queue = [];
    private long _clockTicks;
    private long? _originClockTicks;
    private long _timestamp;
    private long _nextId;

    public DateTimeOffset Now => new(_clockTicks, TimeSpan.Zero);

    public long Timestamp
    {
        get
        {
            _originClockTicks ??= _clockTicks;
            return _timestamp;
        }
    }

    public void Schedule(IWorkItem item) => Schedule(item, _timestamp);

    public void Schedule(IWorkItem item, long dueTimestamp)
    {
        _queue.Add(new ScheduledItem(dueTimestamp, _nextId++, item));
        _queue.Sort(static (left, right) =>
        {
            var due = left.DueTimestamp.CompareTo(right.DueTimestamp);
            return due != 0 ? due : left.Id.CompareTo(right.Id);
        });
    }

    public void AdvanceBy(long ticks)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }

        AdvanceTo(_clockTicks + ticks);
    }

    public void AdvanceTo(long ticks)
    {
        if (ticks < _clockTicks)
        {
            return;
        }

        _originClockTicks ??= _queue.Count == 0 ? ticks : _clockTicks;
        var targetTimestamp = ToTimestamp(ticks);
        while (_queue.Count != 0 && _queue[0].DueTimestamp <= targetTimestamp)
        {
            var next = _queue[0];
            _queue.RemoveAt(0);
            _timestamp = next.DueTimestamp;
            _clockTicks = ToClockTicks(_timestamp);
            next.Item.Execute();
        }

        _clockTicks = ticks;
        _timestamp = targetTimestamp;
    }

    public void Start()
    {
        while (_queue.Count != 0)
        {
            var next = _queue[0];
            AdvanceTo(ToClockTicks(next.DueTimestamp));
        }
    }

    private long ToTimestamp(long ticks)
    {
        _originClockTicks ??= ticks;
        var elapsedTicks = ticks - _originClockTicks.Value;
        if (elapsedTicks <= 0)
        {
            return 0;
        }

        var value = elapsedTicks / (double)TimeSpan.TicksPerSecond * Stopwatch.Frequency;
        return value >= long.MaxValue ? long.MaxValue : (long)value;
    }

    private long ToClockTicks(long timestamp)
    {
        var originClockTicks = _originClockTicks ?? 0;
        if (timestamp <= 0)
        {
            return originClockTicks;
        }

        var value = timestamp / (double)Stopwatch.Frequency * TimeSpan.TicksPerSecond;
        return value >= long.MaxValue - originClockTicks ? long.MaxValue : originClockTicks + (long)value;
    }

    private sealed record ScheduledItem(long DueTimestamp, long Id, IWorkItem Item);
}
