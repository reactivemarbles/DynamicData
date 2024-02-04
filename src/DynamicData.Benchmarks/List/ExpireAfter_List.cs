using System;
using System.Collections.Generic;
using System.Linq;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.List;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ExpireAfter_List
{
    public ExpireAfter_List()
        => _items = Enumerable
            .Range(0, 1_000)
            .Select(_ => new object())
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
        using var source = new SourceList<object>();

        using var subscription = source
            .ExpireAfter(static _ => TimeSpan.FromMinutes(60), pollingInterval: null)
            .Subscribe();

        for (var i = 0; i < addCount; ++i)
            source.Add(_items[i]);

        var targetCount = addCount - removeCount;
        while (source.Count > targetCount)
            source.RemoveAt(0);

        subscription.Dispose();
    }

    private readonly IReadOnlyList<object> _items;
}
