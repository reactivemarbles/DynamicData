using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class FilterImmutable
{
    private readonly IReadOnlyList<IChangeSet<Item, int>> _addChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item, int>> _replaceChangeSets;
    private readonly IReadOnlyList<IChangeSet<Item, int>> _removeChangeSets;

    public FilterImmutable()
    {
        var source = new ChangeAwareCache<Item, int>(capacity: 1_000);

        var addChangeSets = new List<IChangeSet<Item, int>>(capacity: 1_000);
        for (var id = 1; id <= 1_000; ++id)
        {
            source.Add(
                item: new Item()
                {
                    Id = id,
                    IsIncluded = (id % 2) == 0
                },
                key: id);
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
                    IsIncluded = (id % 4) != 0
                },
                key: id);
            replaceChangeSets.Add(source.CaptureChanges());
        }
        _replaceChangeSets = replaceChangeSets;

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
            .FilterImmutable(static item => item.IsIncluded)
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
            .FilterImmutable(static item => item.IsIncluded)
            .Subscribe();

        foreach (var changeSet in _addChangeSets)
            source.OnNext(changeSet);

        foreach (var changeSet in _replaceChangeSets)
            source.OnNext(changeSet);

        source.OnCompleted();
    }

    [Benchmark]
    public void AddsAndRemoves()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .FilterImmutable(static item => item.IsIncluded)
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
            .FilterImmutable(static item => item.IsIncluded)
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

        public required bool IsIncluded { get; init; }
    }
}
