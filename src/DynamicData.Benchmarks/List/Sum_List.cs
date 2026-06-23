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

    [Params(100, 500, 1_000, 10_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var source = new ChangeAwareList<Item>(capacity: Count);

        var addChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var id = 1; id <= Count; ++id)
        {
            source.Add(new Item()
            {
                Id = id,
                Value = id
            });
            addChangeSets.Add(source.CaptureChanges());
        }
        _addChangeSets = addChangeSets;

        var replaceChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var index = 0; index < Count; ++index)
        {
            source[index] = new Item()
            {
                Id = index + 1,
                Value = (index + 1) * 2
            };
            replaceChangeSets.Add(source.CaptureChanges());
        }
        _replaceChangeSets = replaceChangeSets;

        var refreshChangeSets = new List<IChangeSet<Item>>(capacity: Count);
        for (var index = 0; index < Count; ++index)
        {
            // Mutate in place, then refresh - the scenario stateless aggregation cannot currently observe.
            source[index].Value += 1;
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
    }

    [Benchmark]
    public void Adds() => Run(_addChangeSets);

    [Benchmark]
    public void Replaces() => Run(_replaceChangeSets);

    [Benchmark]
    public void Refreshes() => Run(_refreshChangeSets);

    [Benchmark]
    public void Removes() => Run(_removeChangeSets);

    private static void Run(IReadOnlyList<IChangeSet<Item>> changeSets)
    {
        using var source = new Subject<IChangeSet<Item>>();

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
