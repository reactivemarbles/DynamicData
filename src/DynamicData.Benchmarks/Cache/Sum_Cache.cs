using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

using DynamicData.Aggregation;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class Sum_Cache
{
    private IReadOnlyList<IChangeSet<Item, int>> _addChangeSets = null!;
    private IReadOnlyList<IChangeSet<Item, int>> _replaceChangeSets = null!;
    private IReadOnlyList<IChangeSet<Item, int>> _removeChangeSets = null!;
    private IReadOnlyList<IChangeSet<Item, int>> _refreshChangeSets = null!;

    private IChangeSet<Item, int> _seedAfterAdds = null!;
    private IChangeSet<Item, int> _seedAfterReplaces = null!;
    private IChangeSet<Item, int> _seedAfterRefreshes = null!;

    [Params(100, 500, 1_000, 10_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var source = new ChangeAwareCache<Item, int>(capacity: Count);
        var items = new Item[Count + 1];

        var addChangeSets = new List<IChangeSet<Item, int>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            var item = new Item()
            {
                Id = id,
                Value = id
            };
            items[id] = item;
            source.Add(item, key: id);
            addChangeSets.Add(source.CaptureChanges());
        }
        _addChangeSets = addChangeSets;

        var addedItems = (Item[])items.Clone();

        // Each non-add benchmark only forms a valid sequence for a stateful operator after it has seen
        // the preceding population. Seed snapshots are collapsed into one change set so their cost stays
        // outside the measured sequence as far as possible.
        _seedAfterAdds = BuildSeed(addedItems);

        var replaceChangeSets = new List<IChangeSet<Item, int>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            var replacement = new Item()
            {
                Id = id,
                Value = id * 2
            };
            items[id] = replacement;
            source.AddOrUpdate(replacement, key: id);
            replaceChangeSets.Add(source.CaptureChanges());
        }
        _replaceChangeSets = replaceChangeSets;
        _seedAfterReplaces = BuildSeed(items);

        var refreshChangeSets = new List<IChangeSet<Item, int>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            // Mutate in place, then refresh - the scenario stateless aggregation cannot currently observe.
            items[id].Value += 1;
            source.Refresh(id);
            refreshChangeSets.Add(source.CaptureChanges());
        }
        _refreshChangeSets = refreshChangeSets;
        _seedAfterRefreshes = BuildSeed(items);

        var removeChangeSets = new List<IChangeSet<Item, int>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            source.Remove(id);
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

    private static IChangeSet<Item, int> BuildSeed(Item[] items)
    {
        var seed = new ChangeAwareCache<Item, int>(capacity: items.Length - 1);

        for (var id = 1; id < items.Length; ++id)
            seed.Add(new Item()
            {
                Id = items[id].Id,
                Value = items[id].Value
            }, key: id);

        return seed.CaptureChanges();
    }

    private static void Run(IChangeSet<Item, int>? seed, IReadOnlyList<IChangeSet<Item, int>> changeSets)
    {
        using var source = new Subject<IChangeSet<Item, int>>();

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
