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
    private readonly IReadOnlyList<IChangeSet<Item, int>> _addChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item, int>> _replaceChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item, int>> _removeChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item, int>> _refreshChangeSets;

    public Sum_Cache()
    {
        var source = new ChangeAwareCache<Item, int>(capacity: 1_000);
        var items = new Item[1_001];

        var addChangeSets = new List<IChangeSet<Item, int>>(capacity: 1_000);
        for (var id = 1; id <= 1_000; ++id)
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

        var replaceChangeSets = new List<IChangeSet<Item, int>>(capacity: 500);
        for (var id = 2; id <= 1_000; id += 2)
        {
            source.AddOrUpdate(
                item: new Item()
                {
                    Id = id,
                    Value = id * 2
                },
                key: id);
            replaceChangeSets.Add(source.CaptureChanges());
        }
        _replaceChangeSets = replaceChangeSets;

        var refreshChangeSets = new List<IChangeSet<Item, int>>(capacity: 1_000);
        for (var id = 1; id <= 1_000; ++id)
        {
            // Mutate in place, then refresh - the scenario stateless aggregation cannot currently observe.
            items[id].Value += 1;
            source.Refresh(id);
            refreshChangeSets.Add(source.CaptureChanges());
        }
        _refreshChangeSets = refreshChangeSets;

        var removeChangeSets = new List<IChangeSet<Item, int>>(capacity: 1_000);
        for (var id = 1; id <= 1_000; ++id)
        {
            source.Remove(id);
            removeChangeSets.Add(source.CaptureChanges());
        }
        _removeChangeSets = removeChangeSets;
    }

    [Benchmark]
    public void Adds()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Sum(static item => item.Value)
            .Subscribe();

        foreach (var changeSet in _addChangeSets)
            source.OnNext(changeSet);

        source.OnCompleted();
    }

    [Benchmark]
    public void AddsAndReplacements()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Sum(static item => item.Value)
            .Subscribe();

        foreach (var changeSet in _addChangeSets)
            source.OnNext(changeSet);

        foreach (var changeSet in _replaceChangeSets)
            source.OnNext(changeSet);

        source.OnCompleted();
    }

    [Benchmark]
    public void AddsAndRefreshes()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Sum(static item => item.Value)
            .Subscribe();

        foreach (var changeSet in _addChangeSets)
            source.OnNext(changeSet);

        foreach (var changeSet in _refreshChangeSets)
            source.OnNext(changeSet);

        source.OnCompleted();
    }

    [Benchmark]
    public void AddsAndRemoves()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Sum(static item => item.Value)
            .Subscribe();

        foreach (var changeSet in _addChangeSets)
            source.OnNext(changeSet);

        foreach (var changeSet in _removeChangeSets)
            source.OnNext(changeSet);

        source.OnCompleted();
    }

    [Benchmark]
    public void AddsReplacementsAndRemoves()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Sum(static item => item.Value)
            .Subscribe();

        foreach (var changeSet in _addChangeSets)
            source.OnNext(changeSet);

        foreach (var changeSet in _replaceChangeSets)
            source.OnNext(changeSet);

        foreach (var changeSet in _removeChangeSets)
            source.OnNext(changeSet);

        source.OnCompleted();
    }

    private sealed class Item
    {
        public required int Id { get; init; }

        public int Value { get; set; }
    }
}
