using System.Collections.Generic;
using System.Reactive.Concurrency;

namespace DynamicData.Tests.Utilities;

public sealed class ListItemRecordingObserver<T>
        : RecordingObserverBase<IChangeSet<T>>
    where T : notnull
{
    private readonly List<IChangeSet<T>> _recordedChangeSets;
    private readonly List<T> _recordedItems;

    public ListItemRecordingObserver(IScheduler scheduler)
        : base(scheduler)
    {
        _recordedChangeSets = new();
        _recordedItems = new();
    }        

    public IReadOnlyList<IChangeSet<T>> RecordedChangeSets
        => _recordedChangeSets;

    public IReadOnlyList<T> RecordedItems
        => _recordedItems;

    protected override void OnNext(IChangeSet<T> value)
    {
        if (!HasFinalized)
        {
            _recordedChangeSets.Add(value);

            foreach (var change in value)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        if (change.Item.CurrentIndex is -1)
                            _recordedItems.Add(change.Item.Current);
                        else
                            _recordedItems.Insert(change.Item.CurrentIndex, change.Item.Current);
                        break;

                    case ListChangeReason.AddRange:
                        if (change.Range.Index is -1)
                            _recordedItems.AddRange(change.Range);
                        else
                            _recordedItems.InsertRange(change.Range.Index, change.Range);
                        break;

                    case ListChangeReason.Clear:
                        _recordedItems.Clear();
                        break;

                    case ListChangeReason.Moved:
                        _recordedItems.RemoveAt(change.Item.PreviousIndex);
                        _recordedItems.Insert(change.Item.CurrentIndex, change.Item.Current);
                        break;

                    case ListChangeReason.Remove:
                        _recordedItems.RemoveAt(change.Item.CurrentIndex);
                        break;

                    case ListChangeReason.RemoveRange:
                        _recordedItems.RemoveRange(change.Range.Index, change.Range.Count);
                        break;

                    case ListChangeReason.Replace:
                        _recordedItems[change.Item.CurrentIndex] = change.Item.Current;
                        break;
                }
            }
        }
    }
}
