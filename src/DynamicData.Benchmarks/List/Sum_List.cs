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

        var refreshChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var index = 0; index < Count; ++index)
        {
            // Mutate in place, then refresh - the scenario stateless aggregation cannot currently observe.
            items[index].Value += 1;
            source.RefreshAt(index);
            refreshChangeSets.Add(source.CaptureChanges());
        }
        _refreshChangeSets = refreshChangeSets;

        var removeChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            source.RemoveAt(source.Count - 1);
            removeChangeSets.Add(source.CaptureChanges());
        }
        _removeChangeSets = removeChangeSets;

        // Replaces, refreshes, and removes only form a valid sequence for an operator that has already
        // seen the items they refer to, so each of those runs gets seeded with the population as it stood
        // beforehand. Collapsing the seed into a single change set keeps its cost off the measurement as
        // far as possible: replaces follow on from the items that were added, while refreshes and removes
        // follow on from the items that replaced them.
        _seedAfterAdds = BuildSeed(addedItems);
        _seedAfterReplaces = BuildSeed(items);
    }

    [Benchmark]
    public void Adds() => Run(seed: null, _addChangeSets);

    [Benchmark]
    public void Replaces() => Run(_seedAfterAdds, _replaceChangeSets);

    [Benchmark]
    public void Refreshes() => Run(_seedAfterReplaces, _refreshChangeSets);

    [Benchmark]
    public void Removes() => Run(_seedAfterReplaces, _removeChangeSets);

    private static IChangeSet<Item> BuildSeed(Item[] items)
    {
        var seed = new ChangeAwareList<Item>(capacity: items.Length);

        seed.AddRange(items);

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
