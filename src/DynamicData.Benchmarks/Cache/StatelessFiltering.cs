using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class StatelessFiltering
{
    private readonly IReadOnlyList<IChangeSet<Item, int>> _changeSets;

    public StatelessFiltering()
    {
        var source = new ChangeAwareCache<Item, int>(capacity: 1_000);
        var changeSets = new List<IChangeSet<Item, int>>(capacity: 2_500);

        for (var id = 1; id <= 1_000; ++id)
        {
            source.Add(
                item: new Item()
                {
                    Id = id,
                    IsIncluded = (id % 2) == 0
                },
                key: id);
            changeSets.Add(source.CaptureChanges());
        }

        for (var id = 2; id <= 1_000; id += 2)
        {
            source.AddOrUpdate(
                item: new Item()
                {
                    Id = id,
                    IsIncluded = (id % 4) != 0
                },
                key: id);
            changeSets.Add(source.CaptureChanges());
        }

        for (var id = 1; id <= 1_000; ++id)
        {
            source.Remove(id);
            changeSets.Add(source.CaptureChanges());
        }

        _changeSets = changeSets;
    }

    [Benchmark(Baseline = true)]
    public void Filter()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Filter(static item => item.IsIncluded)
            .Subscribe();

        foreach (var changeSet in _changeSets)
            source.OnNext(changeSet);
        source.OnCompleted();
    }

    [Benchmark]
    public void FilterImmutable()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .FilterImmutable(static item => item.IsIncluded)
            .Subscribe();

        foreach (var changeSet in _changeSets)
            source.OnNext(changeSet);
        source.OnCompleted();
    }

    private sealed class Item
    {
        public required int Id { get; init; }

        public required bool IsIncluded { get; init; }
    }
}
