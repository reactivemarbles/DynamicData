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
    private readonly IReadOnlyList<IChangeSet<Item>> _addChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item>> _replaceChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item>> _removeChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item>> _refreshChangeSets;

    public Sum_List()
    {
        var source = new ChangeAwareList<Item>(capacity: 1_000);

        var addChangeSets = new List<IChangeSet<Item>>(capacity: 1_000);
        for (var id = 1; id <= 1_000; ++id)
        {
            source.Add(new Item()
            {
                Id = id,
                Value = id
            });
            addChangeSets.Add(source.CaptureChanges());
        }
        _addChangeSets = addChangeSets;

        var replaceChangeSets = new List<IChangeSet<Item>>(capacity: 500);
        for (var index = 0; index < 1_000; index += 2)
        {
            source[index] = new Item()
            {
                Id = index + 1,
                Value = (index + 1) * 2
            };
            replaceChangeSets.Add(source.CaptureChanges());
        }
        _replaceChangeSets = replaceChangeSets;

        var refreshChangeSets = new List<IChangeSet<Item>>(capacity: 1_000);
        for (var index = 0; index < 1_000; ++index)
        {
            // Mutate in place, then refresh - the scenario stateless aggregation cannot currently observe.
            source[index].Value += 1;
            source.RefreshAt(index);
            refreshChangeSets.Add(source.CaptureChanges());
        }
        _refreshChangeSets = refreshChangeSets;

        var removeChangeSets = new List<IChangeSet<Item>>(capacity: 1_000);
        for (var id = 1; id <= 1_000; ++id)
        {
            source.RemoveAt(source.Count - 1);
            removeChangeSets.Add(source.CaptureChanges());
        }
        _removeChangeSets = removeChangeSets;
    }

    [Benchmark]
    public void Adds()
    {
        using var source = new Subject<IChangeSet<Item>>();

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
        using var source = new Subject<IChangeSet<Item>>();

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
        using var source = new Subject<IChangeSet<Item>>();

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
        using var source = new Subject<IChangeSet<Item>>();

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
        using var source = new Subject<IChangeSet<Item>>();

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
