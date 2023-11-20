using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[HideColumns(Column.Ratio, Column.RatioSD, Column.AllocRatio)]
public class ToObservableChangeSet_Cache
{
    public const int RngSeed
        = 1234567;

    public const int MaxItemCount
        = 1000;

    static ToObservableChangeSet_Cache()
    {
        _items = Enumerable.Range(1, MaxItemCount / 2)
            .Select(id => new Item() { Id = id })
            .ToArray();

        var rng = new Random(RngSeed);

        _itemIndexSequence = Enumerable.Range(1, MaxItemCount)
            .Select(_ => rng.Next(maxValue: _items.Count))
            .ToArray();
    }

    [Benchmark]
    [Arguments(0, -1)]
    [Arguments(0, 0)]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(10, -1)]
    [Arguments(10, 1)]
    [Arguments(10, 5)]
    [Arguments(10, 10)]
    [Arguments(100, -1)]
    [Arguments(100, 10)]
    [Arguments(100, 50)]
    [Arguments(100, 100)]
    [Arguments(1_000, -1)]
    [Arguments(1_000, 100)]
    [Arguments(1_000, 500)]
    [Arguments(1_000, 1_000)]
    public void AddsUpdatesAndFinalization(int itemCount, int sizeLimit)
    {
        using var source = new Subject<Item>();

        using var subscription = source
            .ToObservableChangeSet(
                keySelector: item => item.Id,
                limitSizeTo: sizeLimit)
            .Subscribe();

        var indexModulus = (itemCount / 2) + 1;
        for(var i = 0; i < itemCount; ++i)
            source.OnNext(_items[_itemIndexSequence[i] % indexModulus]);
        source.OnCompleted();
    }

    public class Item
    {
        public int Id { get; init; }
    }

    private static readonly IReadOnlyList<Item>    _items;
    private static readonly IReadOnlyList<int>     _itemIndexSequence;
}
