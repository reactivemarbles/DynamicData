using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class StatelessTransforming
{
    private readonly IReadOnlyList<IChangeSet<Item, int>> _changeSets;

    public StatelessTransforming()
    {
        var source = new ChangeAwareCache<Item, int>(capacity: 1_000);
        var changeSets = new List<IChangeSet<Item, int>>(capacity: 2_500);

        for (var id = 1; id <= 1_000; ++id)
        {
            source.Add(
                item: new Item()
                {
                    Id = id,
                    Name = $"Item #{id}"
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
                    Name = $"Replacement Item #{id}"
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
    public void Transform()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .Transform(static item => item.Name)
            .Subscribe();

        foreach (var changeSet in _changeSets)
            source.OnNext(changeSet);
        source.OnCompleted();
    }

    [Benchmark]
    public void TransformImmutable()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .TransformImmutable(static item => item.Name)
            .Subscribe();

        foreach (var changeSet in _changeSets)
            source.OnNext(changeSet);
        source.OnCompleted();
    }

    private sealed class Item
    {
        public required int Id { get; init; }

        public required string Name { get; init; }
    }
}
