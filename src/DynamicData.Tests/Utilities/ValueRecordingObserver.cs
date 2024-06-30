using System.Collections.Generic;
using System.Reactive.Concurrency;

namespace DynamicData.Tests.Utilities;

public sealed class ValueRecordingObserver<T>
    : RecordingObserverBase<T>
{
    private readonly List<T> _recordedValues;

    public ValueRecordingObserver(IScheduler scheduler)
            : base(scheduler)
        => _recordedValues = new();

    public IReadOnlyList<T> RecordedValues
        => _recordedValues;

    protected override void OnNext(T value)
    {
        if (!HasFinalized)
            _recordedValues.Add(value);
    }
}
