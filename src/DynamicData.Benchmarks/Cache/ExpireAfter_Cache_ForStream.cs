using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ExpireAfter_Cache_ForStream
{
    public ExpireAfter_Cache_ForStream()
    {
        var additions = new List<IChangeSet<Item, int>>(capacity: 1_000);
        var removals = new List<IChangeSet<Item, int>>(capacity: 1_000);

        for (var id = 1; id <= 1_000; ++id)
        {
            var item = new Item()
            {
                Id = id
            };

            additions.Add(new ChangeSet<Item, int>(capacity: 1)
            {
                new(reason: ChangeReason.Add,
                    key: id,
                    current: item)
            });

            removals.Add(new ChangeSet<Item, int>()
            {
                new(reason: ChangeReason.Remove,
                    key: item.Id,
                    current: item)
            });
        }

        _additions = additions;
        _removals = removals;
    }

    [Benchmark]
    [Arguments(1, 0)]
    [Arguments(1, 1)]
    [Arguments(10, 0)]
    [Arguments(10, 1)]
    [Arguments(10, 10)]
    [Arguments(100, 0)]
    [Arguments(100, 1)]
    [Arguments(100, 10)]
    [Arguments(100, 100)]
    [Arguments(1_000, 0)]
    [Arguments(1_000, 1)]
    [Arguments(1_000, 10)]
    [Arguments(1_000, 100)]
    [Arguments(1_000, 1_000)]
    public void AddsRemovesAndFinalization(int addCount, int removeCount)
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .ExpireAfter(static _ => TimeSpan.FromMinutes(60))
            .Subscribe();

        var itemLifetime = TimeSpan.FromMilliseconds(1);

        var itemsToRemove = new List<Item>();

        for (var i = 0; i < addCount; ++i)
            source.OnNext(_additions[i]);

        for (var i = 0; i < removeCount; ++i)
            source.OnNext(_removals[i]);

        subscription.Dispose();
    }

    private readonly IReadOnlyList<IChangeSet<Item, int>> _additions;
    private readonly IReadOnlyList<IChangeSet<Item, int>> _removals;

    private sealed class Item
    {
        public int Id { get; init; }
    }
}
