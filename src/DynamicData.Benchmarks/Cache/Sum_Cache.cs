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

        var refreshChangeSets = new List<IChangeSet<Item, int>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            // Mutate in place, then refresh - the scenario stateless aggregation cannot currently observe.
            items[id].Value += 1;
            source.Refresh(id);
            refreshChangeSets.Add(source.CaptureChanges());
        }
        _refreshChangeSets = refreshChangeSets;

        var removeChangeSets = new List<IChangeSet<Item, int>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            source.Remove(id);
            removeChangeSets.Add(source.CaptureChanges());
        }
        _removeChangeSets = removeChangeSets;
    }

    [Benchmark]
    public void Adds() => Run(_addChangeSets);

    [Benchmark]
    public void Replaces() => Run(_replaceChangeSets);

    [Benchmark]
    public void Refreshes() => Run(_refreshChangeSets);

    [Benchmark]
    public void Removes() => Run(_removeChangeSets);

    private static void Run(IReadOnlyList<IChangeSet<Item, int>> changeSets)
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Sum(static item => item.Value)
            .Subscribe();

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
