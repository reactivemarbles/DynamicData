using System;
using System.Collections.Generic;
using System.Linq;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ExpireAfter_Cache_ForSource
{
    public ExpireAfter_Cache_ForSource()
        => _items = Enumerable
            .Range(1, 1_000)
            .Select(id => new Item()
            {
                Id = id
            })
            .ToArray();

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
        using var source = new SourceCache<Item, int>(static item => item.Id);

        using var subscription = source
            .ExpireAfter(
                timeSelector: static _ => TimeSpan.FromMinutes(60),
                interval: null)
            .Subscribe();

        for (var i = 0; i < addCount; ++i)
            source.AddOrUpdate(_items[i]);

        for (var i = 0; i < removeCount; ++i)
            source.RemoveKey(_items[i].Id);

        subscription.Dispose();
    }

    private readonly IReadOnlyList<Item> _items;

    private sealed class Item
    {
        public int Id { get; init; }
    }
}
