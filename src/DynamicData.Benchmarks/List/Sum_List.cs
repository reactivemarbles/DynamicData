using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

using DynamicData.Aggregation;

namespace DynamicData.Benchmarks.List;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class Sum_List
{
    private IReadOnlyList<IChangeSet<Item>> _addChangeSets = null!;
    private IReadOnlyList<IChangeSet<Item>> _replaceChangeSets = null!;
    private IReadOnlyList<IChangeSet<Item>> _removeChangeSets = null!;
    private IReadOnlyList<IChangeSet<Item>> _refreshChangeSets = null!;

    private IChangeSet<Item> _seedAfterAdds = null!;
    private IChangeSet<Item> _seedAfterReplaces = null!;
    private IChangeSet<Item> _seedAfterRefreshes = null!;

    [Params(100, 500, 1_000, 10_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var source = new ChangeAwareList<Item>(capacity: Count);
        var items = new Item[Count];

        var addChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var index = 0; index < Count; ++index)
        {
            items[index] = new Item()
            {
                Id = index + 1,
                Value = index + 1
            };
            source.Add(items[index]);
            addChangeSets.Add(source.CaptureChanges());
        }
        _addChangeSets = addChangeSets;

        var addedItems = (Item[])items.Clone();

        // Each non-add benchmark only forms a valid sequence for a stateful operator after it has seen
        // the preceding population. Seed snapshots are collapsed into one change set so their cost stays
        // outside the measured sequence as far as possible.
        _seedAfterAdds = BuildSeed(addedItems);

        var replaceChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var index = 0; index < Count; ++index)
        {
            items[index] = new Item()
            {
                Id = index + 1,
                Value = (index + 1) * 2
            };
            source[index] = items[index];
            replaceChangeSets.Add(source.CaptureChanges());
        }
        _replaceChangeSets = replaceChangeSets;
        _seedAfterReplaces = BuildSeed(items);

        var refreshChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var index = 0; index < Count; ++index)
        {
            // Mutate in place, then refresh - the scenario stateless aggregation cannot currently observe.
            items[index].Value += 1;
            source.RefreshAt(index);
            refreshChangeSets.Add(source.CaptureChanges());
        }
        _refreshChangeSets = refreshChangeSets;
        _seedAfterRefreshes = BuildSeed(items);

        var removeChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            source.RemoveAt(source.Count - 1);
            removeChangeSets.Add(source.CaptureChanges());
        }
        _removeChangeSets = removeChangeSets;
    }

    [Benchmark]
    public void Adds() => Run(seed: null, _addChangeSets);

    [Benchmark]
    public void Replaces() => Run(_seedAfterAdds, _replaceChangeSets);

    [Benchmark]
    public void Refreshes() => Run(_seedAfterReplaces, _refreshChangeSets);

    [Benchmark]
    public void Removes() => Run(_seedAfterRefreshes, _removeChangeSets);

    private static IChangeSet<Item> BuildSeed(Item[] items)
    {
        var seed = new ChangeAwareList<Item>(capacity: items.Length);

        foreach (var item in items)
        {
            seed.Add(new Item()
            {
                Id = item.Id,
                Value = item.Value
            });
        }

        return seed.CaptureChanges();
    }

    private static void Run(IChangeSet<Item>? seed, IReadOnlyList<IChangeSet<Item>> changeSets)
    {
        using var source = new Subject<IChangeSet<Item>>();

        using var subscription = source
            .Sum(static item => item.Value)
            .Subscribe();

        if (seed is not null)
            source.OnNext(seed);

        foreach (var changeSet in changeSets)
            source.OnNext(changeSet);

        source.OnCompleted();
    }

    private sealed class Item
    {
        public required int Id { get; init; }

        public int Value { get; set; }
    }
}
