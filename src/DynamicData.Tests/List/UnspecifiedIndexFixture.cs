using System.Collections.Generic;
using System.Linq;

using Bogus;

using DynamicData.Kernel;

namespace DynamicData.Tests.List;

/// <summary>
/// List changes may carry an unspecified index (-1). An operator that keeps a positional copy of its upstream list locates
/// such an existing item by <see cref="EqualityComparer{T}.Default"/>, taking the first match, appends such an addition, and
/// replaces such a replacement in place. <see cref="ObservableCacheEx.RemoveKey{TObject, TKey}(System.IObservable{IChangeSet{TObject, TKey}})"/>
/// emits unindexed changes for any unsorted cache, so these tests cover both that pipeline and hand-built unindexed changesets.
/// </summary>
public sealed partial class UnspecifiedIndexFixture
{
    private const int MaximumEntryCount = 32;

    private const int MinimumEntryCount = 8;

    private const int Seed = 0x1119;

    private readonly Randomizer _randomizer = new(Seed);

    private static Change<T> Add<T>(T item)
            where T : notnull
        => new(ListChangeReason.Add, item);

    private static Change<T> AddRange<T>(params T[] items)
            where T : notnull
        => new(ListChangeReason.AddRange, items);

    private static Change<T> Remove<T>(T item)
            where T : notnull
        => new(ListChangeReason.Remove, item);

    private static Change<T> RemoveRange<T>(params T[] items)
            where T : notnull
        => new(ListChangeReason.RemoveRange, items);

    private static Change<T> Replace<T>(T previous, T current)
            where T : notnull
        => new(ListChangeReason.Replace, current, Optional.Some(previous));

    private int[] GenerateDistinctValues(int count)
    {
        var values = new HashSet<int>();
        while (values.Count < count)
            values.Add(_randomizer.Int());

        return values.ToArray();
    }

    private Entry[] GenerateEntries()
    {
        var count = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        var ids = GenerateDistinctValues(count);
        var values = GenerateDistinctValues(count);

        return ids.Select((id, index) => new Entry(id, values[index], _randomizer.Bool())).ToArray();
    }

    private Entry GenerateIncludedEntry(IEnumerable<Entry> entries) => _randomizer.ArrayElement(entries.Where(static entry => entry.IsIncluded).ToArray());

    private Entry GenerateUpdate(Entry entry, IEnumerable<Entry> entries)
    {
        var values = new HashSet<int>(entries.Select(static entry => entry.Value));

        var value = _randomizer.Int();
        while (values.Contains(value))
            value = _randomizer.Int();

        return entry with { Value = value, IsIncluded = !entry.IsIncluded };
    }

    private readonly record struct Entry(int Id, int Value, bool IsIncluded);

    private sealed record Entity(int Id, int Value);
}
