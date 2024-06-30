using System.Collections.Generic;
using System.Reactive.Concurrency;

namespace DynamicData.Tests.Utilities;

public sealed class CacheItemRecordingObserver<TObject, TKey>
        : RecordingObserverBase<IChangeSet<TObject, TKey>>
    where TObject : notnull
    where TKey : notnull
{
    private readonly List<IChangeSet<TObject, TKey>> _recordedChangeSets;
    private readonly Dictionary<TKey, TObject> _recordedItemsByKey;
    private readonly List<TObject> _recordedItemsSorted;

    public CacheItemRecordingObserver(IScheduler scheduler)
        : base(scheduler)
    {
        _recordedChangeSets = new();
        _recordedItemsByKey = new();
        _recordedItemsSorted = new();
    }        

    public IReadOnlyList<IChangeSet<TObject, TKey>> RecordedChangeSets
        => _recordedChangeSets;

    public IReadOnlyDictionary<TKey, TObject> RecordedItemsByKey
        => _recordedItemsByKey;

    public IReadOnlyList<TObject> RecordedItemsSorted
        => _recordedItemsSorted;

    protected override void OnNext(IChangeSet<TObject, TKey> value)
    {
        if (!HasFinalized)
        {
            _recordedChangeSets.Add(value);

            foreach (var change in value)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        _recordedItemsByKey.Add(change.Key, change.Current);
                        if (change.CurrentIndex is not -1)
                            _recordedItemsSorted.Insert(change.CurrentIndex, change.Current);
                        break;

                    case ChangeReason.Moved:
                        _recordedItemsSorted.RemoveAt(change.PreviousIndex);
                        _recordedItemsSorted.Insert(change.CurrentIndex, change.Current);
                        break;

                    case ChangeReason.Remove:
                        _recordedItemsByKey.Remove(change.Key);
                        if (change.CurrentIndex is not -1)
                            _recordedItemsSorted.RemoveAt(change.CurrentIndex);
                        break;

                    case ChangeReason.Update:
                        _recordedItemsByKey[change.Key] = change.Current;
                        if (change.CurrentIndex is not -1)
                        {
                            _recordedItemsSorted.RemoveAt(change.PreviousIndex);
                            _recordedItemsSorted.Insert(change.CurrentIndex, change.Current);
                        }
                        break;
                }
            }
        }
    }
}
